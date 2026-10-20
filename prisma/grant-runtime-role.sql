-- Run with psql as the migration owner after creating a restricted login role:
-- psql "$DIRECT_URL" -v ON_ERROR_STOP=1 -v runtime_role=melife_app -f prisma/grant-runtime-role.sql
-- Passwords are provisioned in the hosting provider, not in this file.
BEGIN;
GRANT CONNECT ON DATABASE :"DBNAME" TO :"runtime_role";
GRANT USAGE ON SCHEMA app TO :"runtime_role";
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA app TO :"runtime_role";
ALTER DEFAULT PRIVILEGES IN SCHEMA app
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO :"runtime_role";
COMMIT;
