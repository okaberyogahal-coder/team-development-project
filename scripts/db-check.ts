import { loadEnvConfig } from "@next/env";
import { createDatabase } from "../server/db/connection";

loadEnvConfig(process.cwd());

async function main() {
  const url = process.env.DATABASE_URL;
  if (!url) throw new Error("DATABASE_URL is required. See docs/database.md.");
  const database = createDatabase(url);
  try {
    await database.$queryRaw`SELECT 1`;
    await database.user.count();
    console.log("PostgreSQL connection and app schema: OK");
  } finally {
    await database.$disconnect();
  }
}

main().catch(() => {
  console.error("Database check failed. Check the URL, PostgreSQL, and migrations (docs/database.md).");
  process.exitCode = 1;
});
