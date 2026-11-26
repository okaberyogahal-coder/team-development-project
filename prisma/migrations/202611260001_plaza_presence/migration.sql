CREATE TABLE app.plaza_presence (
  session_id uuid PRIMARY KEY REFERENCES app.auth_sessions(id) ON DELETE CASCADE,
  last_seen_at timestamptz(3) NOT NULL
);

CREATE INDEX plaza_presence_last_seen_at_idx ON app.plaza_presence (last_seen_at);

REVOKE ALL ON app.plaza_presence FROM PUBLIC;
