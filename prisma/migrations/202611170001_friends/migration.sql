CREATE TYPE app."FriendConnectionStatus" AS ENUM ('PENDING', 'ACCEPTED', 'DECLINED');

CREATE TABLE app.friend_connections (
  user_a_id uuid NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
  user_b_id uuid NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
  requested_by_id uuid NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
  status app."FriendConnectionStatus" NOT NULL DEFAULT 'PENDING',
  created_at timestamptz(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at timestamptz(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT friend_connections_pkey PRIMARY KEY (user_a_id, user_b_id),
  CONSTRAINT friend_connections_ordered_users CHECK (user_a_id < user_b_id),
  CONSTRAINT friend_connections_requester CHECK (requested_by_id IN (user_a_id, user_b_id))
);

CREATE INDEX friend_connections_user_a_status_created_idx
  ON app.friend_connections (user_a_id, status, created_at);
CREATE INDEX friend_connections_user_b_status_created_idx
  ON app.friend_connections (user_b_id, status, created_at);

REVOKE ALL ON app.friend_connections FROM PUBLIC;
