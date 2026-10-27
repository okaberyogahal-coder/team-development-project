-- AlterTable
ALTER TABLE "app"."users" ADD COLUMN     "email_verified" BOOLEAN NOT NULL DEFAULT false,
ADD COLUMN     "image" TEXT,
ADD COLUMN     "name" VARCHAR(20) NOT NULL DEFAULT '';

-- CreateTable
CREATE TABLE "app"."auth_accounts" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "account_id" TEXT NOT NULL,
    "provider_id" TEXT NOT NULL,
    "user_id" UUID NOT NULL,
    "password" TEXT,
    "access_token" TEXT,
    "refresh_token" TEXT,
    "id_token" TEXT,
    "access_token_expires_at" TIMESTAMPTZ(3),
    "refresh_token_expires_at" TIMESTAMPTZ(3),
    "scope" TEXT,
    "created_at" TIMESTAMPTZ(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" TIMESTAMPTZ(3) NOT NULL,

    CONSTRAINT "auth_accounts_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "app"."auth_sessions" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "token" TEXT NOT NULL,
    "user_id" UUID NOT NULL,
    "expires_at" TIMESTAMPTZ(3) NOT NULL,
    "ip_address" TEXT,
    "user_agent" TEXT,
    "created_at" TIMESTAMPTZ(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" TIMESTAMPTZ(3) NOT NULL,

    CONSTRAINT "auth_sessions_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "app"."auth_verifications" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "identifier" TEXT NOT NULL,
    "value" TEXT NOT NULL,
    "expires_at" TIMESTAMPTZ(3) NOT NULL,
    "created_at" TIMESTAMPTZ(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" TIMESTAMPTZ(3) NOT NULL,

    CONSTRAINT "auth_verifications_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "app"."auth_rate_limits" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "key" TEXT NOT NULL,
    "count" INTEGER NOT NULL,
    "last_request" BIGINT NOT NULL,

    CONSTRAINT "auth_rate_limits_pkey" PRIMARY KEY ("id")
);

-- CreateIndex
CREATE INDEX "auth_accounts_user_id_idx" ON "app"."auth_accounts"("user_id");

-- CreateIndex
CREATE UNIQUE INDEX "auth_accounts_provider_id_account_id_key" ON "app"."auth_accounts"("provider_id", "account_id");

-- CreateIndex
CREATE UNIQUE INDEX "auth_sessions_token_key" ON "app"."auth_sessions"("token");

-- CreateIndex
CREATE INDEX "auth_sessions_user_id_idx" ON "app"."auth_sessions"("user_id");

-- CreateIndex
CREATE INDEX "auth_sessions_expires_at_idx" ON "app"."auth_sessions"("expires_at");

-- CreateIndex
CREATE INDEX "auth_verifications_identifier_idx" ON "app"."auth_verifications"("identifier");

-- CreateIndex
CREATE UNIQUE INDEX "auth_rate_limits_key_key" ON "app"."auth_rate_limits"("key");

-- CreateIndex
CREATE INDEX "auth_rate_limits_last_request_idx" ON "app"."auth_rate_limits"("last_request");

-- AddForeignKey
ALTER TABLE "app"."auth_accounts" ADD CONSTRAINT "auth_accounts_user_id_fkey" FOREIGN KEY ("user_id") REFERENCES "app"."users"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "app"."auth_sessions" ADD CONSTRAINT "auth_sessions_user_id_fkey" FOREIGN KEY ("user_id") REFERENCES "app"."users"("id") ON DELETE CASCADE ON UPDATE CASCADE;
