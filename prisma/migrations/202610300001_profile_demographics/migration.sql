-- Existing profiles must complete these fields on their next profile edit.
ALTER TABLE "app"."user_profiles" ADD COLUMN "gender" VARCHAR(6);
ALTER TABLE "app"."user_profiles" ADD CONSTRAINT "user_profiles_gender_check" CHECK ("gender" IS NULL OR "gender" IN ('male', 'female'));
-- NOT VALID keeps legacy age records readable without silently rewriting them.
ALTER TABLE "app"."user_profiles" ADD CONSTRAINT "user_profiles_adult_age_check" CHECK ("age" IS NULL OR "age" BETWEEN 18 AND 130) NOT VALID;
