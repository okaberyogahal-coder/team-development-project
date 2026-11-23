ALTER TABLE "app"."user_profiles"
  ADD COLUMN "avatar_appearance" JSONB,
  ADD COLUMN "avatar_version" INTEGER NOT NULL DEFAULT 0;
