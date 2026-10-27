-- CreateSchema
CREATE SCHEMA IF NOT EXISTS "app";

-- CreateEnum
CREATE TYPE "app"."AccountStatus" AS ENUM ('PENDING', 'ACTIVE', 'SUSPENDED');

-- CreateEnum
CREATE TYPE "app"."MaterialKind" AS ENUM ('VIDEO', 'DOCUMENT');

-- CreateEnum
CREATE TYPE "app"."MediaStatus" AS ENUM ('UPLOADING', 'PROCESSING', 'READY', 'FAILED');

-- CreateEnum
CREATE TYPE "app"."ContentStatus" AS ENUM ('DRAFT', 'PUBLISHED', 'HIDDEN');

-- CreateEnum
CREATE TYPE "app"."ProgressStatus" AS ENUM ('IN_PROGRESS', 'COMPLETED');

-- CreateEnum
CREATE TYPE "app"."LectureSessionStatus" AS ENUM ('DRAFT', 'OPEN', 'ONGOING', 'ENDED', 'CANCELLED');

-- CreateEnum
CREATE TYPE "app"."BookingStatus" AS ENUM ('CONFIRMED', 'CANCELLED');

-- CreateTable
CREATE TABLE "app"."users" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "email" VARCHAR(254),
    "status" "app"."AccountStatus" NOT NULL DEFAULT 'PENDING',
    "created_at" TIMESTAMPTZ(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" TIMESTAMPTZ(3) NOT NULL,
    "deleted_at" TIMESTAMPTZ(3),

    CONSTRAINT "users_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "app"."auth_identities" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "user_id" UUID NOT NULL,
    "provider" VARCHAR(64) NOT NULL,
    "subject" VARCHAR(255) NOT NULL,
    "created_at" TIMESTAMPTZ(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "auth_identities_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "app"."user_profiles" (
    "user_id" UUID NOT NULL,
    "nickname" VARCHAR(20) NOT NULL,
    "bio" VARCHAR(2000) NOT NULL DEFAULT '',
    "avatar_id" VARCHAR(64),
    "age" SMALLINT,
    "age_updated_at" TIMESTAMPTZ(3),
    "teaches" TEXT[] DEFAULT ARRAY[]::TEXT[],
    "wants_to_learn" TEXT[] DEFAULT ARRAY[]::TEXT[],
    "version" INTEGER NOT NULL DEFAULT 1,
    "updated_at" TIMESTAMPTZ(3) NOT NULL,

    CONSTRAINT "user_profiles_pkey" PRIMARY KEY ("user_id")
);

-- CreateTable
CREATE TABLE "app"."learning_preferences" (
    "user_id" UUID NOT NULL,
    "interests" TEXT[] DEFAULT ARRAY[]::TEXT[],
    "strengths" VARCHAR(500) NOT NULL DEFAULT '',
    "goal" VARCHAR(500) NOT NULL DEFAULT '',
    "use_age_for_ai" BOOLEAN NOT NULL DEFAULT false,
    "version" INTEGER NOT NULL DEFAULT 1,
    "updated_at" TIMESTAMPTZ(3) NOT NULL,

    CONSTRAINT "learning_preferences_pkey" PRIMARY KEY ("user_id")
);

-- CreateTable
CREATE TABLE "app"."qualifications" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "user_id" UUID NOT NULL,
    "name" VARCHAR(200) NOT NULL,
    "issuer" VARCHAR(200),
    "acquired_month" VARCHAR(7),
    "created_at" TIMESTAMPTZ(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "qualifications_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "app"."policy_consents" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "user_id" UUID NOT NULL,
    "policy" VARCHAR(64) NOT NULL,
    "version" VARCHAR(64) NOT NULL,
    "accepted_at" TIMESTAMPTZ(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "policy_consents_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "app"."media_assets" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "owner_id" UUID NOT NULL,
    "storage_key" VARCHAR(1024) NOT NULL,
    "kind" "app"."MaterialKind" NOT NULL,
    "mime_type" VARCHAR(127) NOT NULL,
    "byte_size" BIGINT,
    "status" "app"."MediaStatus" NOT NULL DEFAULT 'UPLOADING',
    "created_at" TIMESTAMPTZ(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" TIMESTAMPTZ(3) NOT NULL,

    CONSTRAINT "media_assets_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "app"."learning_materials" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "author_id" UUID NOT NULL,
    "title" VARCHAR(100) NOT NULL,
    "description" VARCHAR(2000) NOT NULL DEFAULT '',
    "category" VARCHAR(100) NOT NULL,
    "tags" TEXT[] DEFAULT ARRAY[]::TEXT[],
    "kind" "app"."MaterialKind" NOT NULL,
    "status" "app"."ContentStatus" NOT NULL DEFAULT 'DRAFT',
    "media_asset_id" UUID,
    "version" INTEGER NOT NULL DEFAULT 1,
    "created_at" TIMESTAMPTZ(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" TIMESTAMPTZ(3) NOT NULL,
    "deleted_at" TIMESTAMPTZ(3),

    CONSTRAINT "learning_materials_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "app"."material_progress" (
    "user_id" UUID NOT NULL,
    "material_id" UUID NOT NULL,
    "material_version" INTEGER NOT NULL,
    "title_snapshot" VARCHAR(100) NOT NULL,
    "status" "app"."ProgressStatus" NOT NULL DEFAULT 'IN_PROGRESS',
    "started_at" TIMESTAMPTZ(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "completed_at" TIMESTAMPTZ(3),

    CONSTRAINT "material_progress_pkey" PRIMARY KEY ("user_id","material_id")
);

-- CreateTable
CREATE TABLE "app"."lectures" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "author_id" UUID NOT NULL,
    "title" VARCHAR(100) NOT NULL,
    "description" VARCHAR(2000) NOT NULL DEFAULT '',
    "prerequisites" VARCHAR(2000) NOT NULL DEFAULT '',
    "preparation" VARCHAR(2000) NOT NULL DEFAULT '',
    "status" "app"."ContentStatus" NOT NULL DEFAULT 'DRAFT',
    "version" INTEGER NOT NULL DEFAULT 1,
    "created_at" TIMESTAMPTZ(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" TIMESTAMPTZ(3) NOT NULL,
    "deleted_at" TIMESTAMPTZ(3),

    CONSTRAINT "lectures_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "app"."lecture_sessions" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "lecture_id" UUID NOT NULL,
    "starts_at" TIMESTAMPTZ(3) NOT NULL,
    "ends_at" TIMESTAMPTZ(3) NOT NULL,
    "capacity" INTEGER NOT NULL,
    "status" "app"."LectureSessionStatus" NOT NULL DEFAULT 'DRAFT',
    "version" INTEGER NOT NULL DEFAULT 1,
    "created_at" TIMESTAMPTZ(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" TIMESTAMPTZ(3) NOT NULL,

    CONSTRAINT "lecture_sessions_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "app"."lecture_bookings" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "session_id" UUID NOT NULL,
    "user_id" UUID NOT NULL,
    "status" "app"."BookingStatus" NOT NULL DEFAULT 'CONFIRMED',
    "created_at" TIMESTAMPTZ(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" TIMESTAMPTZ(3) NOT NULL,
    "cancelled_at" TIMESTAMPTZ(3),
    "attended_at" TIMESTAMPTZ(3),
    "completed_at" TIMESTAMPTZ(3),

    CONSTRAINT "lecture_bookings_pkey" PRIMARY KEY ("id")
);

-- CreateIndex
CREATE UNIQUE INDEX "users_email_key" ON "app"."users"("email");

-- CreateIndex
CREATE INDEX "auth_identities_user_id_idx" ON "app"."auth_identities"("user_id");

-- CreateIndex
CREATE UNIQUE INDEX "auth_identities_provider_subject_key" ON "app"."auth_identities"("provider", "subject");

-- CreateIndex
CREATE INDEX "qualifications_user_id_idx" ON "app"."qualifications"("user_id");

-- CreateIndex
CREATE UNIQUE INDEX "policy_consents_user_id_policy_version_key" ON "app"."policy_consents"("user_id", "policy", "version");

-- CreateIndex
CREATE UNIQUE INDEX "media_assets_storage_key_key" ON "app"."media_assets"("storage_key");

-- CreateIndex
CREATE INDEX "media_assets_owner_id_status_idx" ON "app"."media_assets"("owner_id", "status");

-- CreateIndex
CREATE UNIQUE INDEX "media_assets_id_owner_id_key" ON "app"."media_assets"("id", "owner_id");

-- CreateIndex
CREATE INDEX "learning_materials_status_deleted_at_created_at_idx" ON "app"."learning_materials"("status", "deleted_at", "created_at");

-- CreateIndex
CREATE INDEX "learning_materials_author_id_created_at_idx" ON "app"."learning_materials"("author_id", "created_at");

-- CreateIndex
CREATE INDEX "learning_materials_media_asset_id_author_id_idx" ON "app"."learning_materials"("media_asset_id", "author_id");

-- CreateIndex
CREATE INDEX "material_progress_user_id_completed_at_idx" ON "app"."material_progress"("user_id", "completed_at");

-- CreateIndex
CREATE INDEX "material_progress_material_id_idx" ON "app"."material_progress"("material_id");

-- CreateIndex
CREATE INDEX "lectures_author_id_created_at_idx" ON "app"."lectures"("author_id", "created_at");

-- CreateIndex
CREATE INDEX "lecture_sessions_status_starts_at_idx" ON "app"."lecture_sessions"("status", "starts_at");

-- CreateIndex
CREATE INDEX "lecture_sessions_lecture_id_starts_at_idx" ON "app"."lecture_sessions"("lecture_id", "starts_at");

-- CreateIndex
CREATE INDEX "lecture_bookings_session_id_status_idx" ON "app"."lecture_bookings"("session_id", "status");

-- CreateIndex
CREATE INDEX "lecture_bookings_user_id_status_idx" ON "app"."lecture_bookings"("user_id", "status");

-- CreateIndex
CREATE UNIQUE INDEX "lecture_bookings_session_id_user_id_key" ON "app"."lecture_bookings"("session_id", "user_id");

-- AddForeignKey
ALTER TABLE "app"."auth_identities" ADD CONSTRAINT "auth_identities_user_id_fkey" FOREIGN KEY ("user_id") REFERENCES "app"."users"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "app"."user_profiles" ADD CONSTRAINT "user_profiles_user_id_fkey" FOREIGN KEY ("user_id") REFERENCES "app"."users"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "app"."learning_preferences" ADD CONSTRAINT "learning_preferences_user_id_fkey" FOREIGN KEY ("user_id") REFERENCES "app"."users"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "app"."qualifications" ADD CONSTRAINT "qualifications_user_id_fkey" FOREIGN KEY ("user_id") REFERENCES "app"."user_profiles"("user_id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "app"."policy_consents" ADD CONSTRAINT "policy_consents_user_id_fkey" FOREIGN KEY ("user_id") REFERENCES "app"."users"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "app"."media_assets" ADD CONSTRAINT "media_assets_owner_id_fkey" FOREIGN KEY ("owner_id") REFERENCES "app"."users"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "app"."learning_materials" ADD CONSTRAINT "learning_materials_author_id_fkey" FOREIGN KEY ("author_id") REFERENCES "app"."users"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "app"."learning_materials" ADD CONSTRAINT "learning_materials_media_asset_id_author_id_fkey" FOREIGN KEY ("media_asset_id", "author_id") REFERENCES "app"."media_assets"("id", "owner_id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "app"."material_progress" ADD CONSTRAINT "material_progress_user_id_fkey" FOREIGN KEY ("user_id") REFERENCES "app"."users"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "app"."material_progress" ADD CONSTRAINT "material_progress_material_id_fkey" FOREIGN KEY ("material_id") REFERENCES "app"."learning_materials"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "app"."lectures" ADD CONSTRAINT "lectures_author_id_fkey" FOREIGN KEY ("author_id") REFERENCES "app"."users"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "app"."lecture_sessions" ADD CONSTRAINT "lecture_sessions_lecture_id_fkey" FOREIGN KEY ("lecture_id") REFERENCES "app"."lectures"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "app"."lecture_bookings" ADD CONSTRAINT "lecture_bookings_session_id_fkey" FOREIGN KEY ("session_id") REFERENCES "app"."lecture_sessions"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "app"."lecture_bookings" ADD CONSTRAINT "lecture_bookings_user_id_fkey" FOREIGN KEY ("user_id") REFERENCES "app"."users"("id") ON DELETE CASCADE ON UPDATE CASCADE;

