-- The private app schema is not an automatically exposed public API.
REVOKE ALL ON SCHEMA app FROM PUBLIC;
REVOKE ALL ON ALL TABLES IN SCHEMA app FROM PUBLIC;
ALTER DEFAULT PRIVILEGES IN SCHEMA app REVOKE ALL ON TABLES FROM PUBLIC;
ALTER DEFAULT PRIVILEGES IN SCHEMA app REVOKE ALL ON FUNCTIONS FROM PUBLIC;

ALTER TABLE app.users ADD CONSTRAINT users_normalized_email
  CHECK (email IS NULL OR (email = lower(btrim(email)) AND email ~ '^[^[:space:]@]+@[^[:space:]@]+\.[^[:space:]@]+$'));
ALTER TABLE app.users ADD CONSTRAINT users_deleted_not_active
  CHECK (deleted_at IS NULL OR status <> 'ACTIVE');
ALTER TABLE app.auth_identities ADD CONSTRAINT identities_not_blank
  CHECK (length(btrim(provider)) > 0 AND length(btrim(subject)) > 0);
ALTER TABLE app.user_profiles ADD CONSTRAINT profiles_valid_fields
  CHECK (length(btrim(nickname)) BETWEEN 1 AND 20 AND (age IS NULL OR age >= 0) AND version > 0);
ALTER TABLE app.learning_preferences ADD CONSTRAINT preferences_valid_version CHECK (version > 0);
ALTER TABLE app.qualifications ADD CONSTRAINT qualifications_valid_fields
  CHECK (length(btrim(name)) > 0 AND (acquired_month IS NULL OR acquired_month ~ '^[0-9]{4}-(0[1-9]|1[0-2])$'));
ALTER TABLE app.policy_consents ADD CONSTRAINT consents_not_blank
  CHECK (length(btrim(policy)) > 0 AND length(btrim(version)) > 0);
ALTER TABLE app.media_assets ADD CONSTRAINT media_valid_fields
  CHECK (length(btrim(storage_key)) > 0 AND length(btrim(mime_type)) > 0 AND (byte_size IS NULL OR byte_size >= 0));
ALTER TABLE app.learning_materials ADD CONSTRAINT materials_valid_fields
  CHECK (length(btrim(title)) > 0 AND length(btrim(category)) > 0 AND version > 0);
ALTER TABLE app.learning_materials ADD CONSTRAINT materials_published_has_media
  CHECK (status <> 'PUBLISHED' OR (media_asset_id IS NOT NULL AND deleted_at IS NULL AND length(btrim(description)) > 0));
ALTER TABLE app.material_progress ADD CONSTRAINT progress_valid_fields
  CHECK (material_version > 0 AND length(btrim(title_snapshot)) > 0
    AND ((status = 'COMPLETED' AND completed_at IS NOT NULL) OR (status = 'IN_PROGRESS' AND completed_at IS NULL))
    AND (completed_at IS NULL OR completed_at >= started_at));
ALTER TABLE app.lectures ADD CONSTRAINT lectures_valid_fields
  CHECK (length(btrim(title)) > 0 AND version > 0 AND (status <> 'PUBLISHED' OR (deleted_at IS NULL AND length(btrim(description)) > 0)));
ALTER TABLE app.lecture_sessions ADD CONSTRAINT sessions_valid_dates_and_capacity
  CHECK (ends_at > starts_at AND capacity > 0 AND version > 0);
ALTER TABLE app.lecture_bookings ADD CONSTRAINT bookings_cancellation_state
  CHECK ((status = 'CANCELLED' AND cancelled_at IS NOT NULL) OR (status = 'CONFIRMED' AND cancelled_at IS NULL));
ALTER TABLE app.lecture_bookings ADD CONSTRAINT bookings_completion_after_attendance
  CHECK (completed_at IS NULL OR (attended_at IS NOT NULL AND completed_at >= attended_at));

-- All booking writers serialize on the session, including independent app instances.
-- Database time is read AFTER taking the row lock; the exact closing boundary is rejected.
CREATE FUNCTION app.enforce_booking() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog AS $$
DECLARE
  target app.lecture_sessions%ROWTYPE;
  teacher uuid;
  lecture_visible boolean;
BEGIN
  IF TG_OP = 'UPDATE' AND (NEW.session_id <> OLD.session_id OR NEW.user_id <> OLD.user_id) THEN
    RAISE EXCEPTION 'BOOKING_IDENTITY_IMMUTABLE' USING ERRCODE = '23514';
  END IF;
  -- A real row update also makes stale repeatable-read writers fail serialization.
  UPDATE app.lecture_sessions SET version = version + 1 WHERE id = NEW.session_id RETURNING * INTO target;
  IF NOT FOUND THEN
    RAISE EXCEPTION 'SESSION_NOT_FOUND' USING ERRCODE = '23503';
  END IF;
  IF NEW.status <> 'CONFIRMED' THEN RETURN NEW; END IF;
  IF TG_OP = 'UPDATE' AND OLD.status = 'CONFIRMED' THEN RETURN NEW; END IF;
  IF NOT EXISTS (SELECT 1 FROM app.users WHERE id = NEW.user_id AND status = 'ACTIVE' AND deleted_at IS NULL) THEN
    RAISE EXCEPTION 'ACCOUNT_NOT_ACTIVE' USING ERRCODE = '23514';
  END IF;
  SELECT author_id, status = 'PUBLISHED' AND deleted_at IS NULL INTO teacher, lecture_visible
    FROM app.lectures WHERE id = target.lecture_id FOR SHARE;
  IF teacher = NEW.user_id THEN
    RAISE EXCEPTION 'TEACHER_CANNOT_BOOK_OWN_SESSION' USING ERRCODE = '23514';
  END IF;
  IF NOT lecture_visible OR target.status <> 'OPEN' THEN
    RAISE EXCEPTION 'SESSION_NOT_OPEN' USING ERRCODE = '23514';
  END IF;
  IF clock_timestamp() >= target.starts_at - interval '30 minutes' THEN
    RAISE EXCEPTION 'BOOKING_CLOSED' USING ERRCODE = '23514';
  END IF;
  IF (SELECT count(*) FROM app.lecture_bookings WHERE session_id = target.id AND status = 'CONFIRMED' AND id <> NEW.id) >= target.capacity THEN
    RAISE EXCEPTION 'SESSION_FULL' USING ERRCODE = '23514';
  END IF;
  RETURN NEW;
END;
$$;

CREATE TRIGGER enforce_booking BEFORE INSERT OR UPDATE ON app.lecture_bookings
  FOR EACH ROW EXECUTE FUNCTION app.enforce_booking();

CREATE FUNCTION app.protect_booked_session() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog AS $$
DECLARE reserved bigint;
BEGIN
  SELECT count(*) INTO reserved FROM app.lecture_bookings WHERE session_id = OLD.id AND status = 'CONFIRMED';
  IF NEW.capacity < reserved THEN
    RAISE EXCEPTION 'CAPACITY_BELOW_BOOKINGS' USING ERRCODE = '23514';
  END IF;
  IF reserved > 0 AND (NEW.starts_at <> OLD.starts_at OR NEW.ends_at <> OLD.ends_at OR NEW.lecture_id <> OLD.lecture_id) THEN
    RAISE EXCEPTION 'BOOKED_SCHEDULE_IMMUTABLE' USING ERRCODE = '23514';
  END IF;
  RETURN NEW;
END;
$$;

CREATE TRIGGER protect_booked_session BEFORE UPDATE ON app.lecture_sessions
  FOR EACH ROW EXECUTE FUNCTION app.protect_booked_session();

REVOKE ALL ON FUNCTION app.enforce_booking() FROM PUBLIC;
REVOKE ALL ON FUNCTION app.protect_booked_session() FROM PUBLIC;
