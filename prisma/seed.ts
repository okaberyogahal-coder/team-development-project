import { loadEnvConfig } from "@next/env";
import { createDatabase } from "../server/db/connection";

loadEnvConfig(process.cwd());

async function main() {
  const connectionString = process.env.DATABASE_URL;
  if (!connectionString) throw new Error("DATABASE_URL is required.");
  const url = new URL(connectionString);
  if (!['localhost', '127.0.0.1', '[::1]'].includes(url.hostname) || !['/melife', '/melife_test'].includes(url.pathname)) {
    throw new Error("Demo seed only runs against the local melife or melife_test database.");
  }
  const database = createDatabase(connectionString);
  try {
    await database.user.upsert({
      where: { email: "demo@example.invalid" },
      update: {},
      create: {
        email: "demo@example.invalid",
        // No identity/password is created; this is not a usable login account.
        profile: { create: { nickname: "デモ利用者", teaches: ["園芸"], wantsToLearn: ["写真"] } },
        preferences: { create: { interests: ["園芸", "写真"], goal: "写真好きな仲間をつくる" } },
      },
    });
    console.log("Local demo profile created (idempotent; login is not enabled).");
  } finally {
    await database.$disconnect();
  }
}

main().catch(() => {
  console.error("Seed failed. Use a migrated local melife database (docs/database.md).");
  process.exitCode = 1;
});
