import { PrismaPg } from "@prisma/adapter-pg";
import { PrismaClient } from "../../generated/prisma/client";

/** Each Node process owns a bounded pool. Import client.ts from Next.js server code. */
export function createDatabase(connectionString: string) {
  const url = new URL(connectionString);
  if (!["postgres:", "postgresql:"].includes(url.protocol)) {
    throw new Error("DATABASE_URL must be a PostgreSQL connection URL.");
  }
  const adapter = new PrismaPg({
    connectionString,
    max: 5,
    connectionTimeoutMillis: 5000,
    idleTimeoutMillis: 30000,
  });
  // Callers handle failures; do not automatically log queries or personal data.
  return new PrismaClient({ adapter, log: [], errorFormat: "minimal" });
}
