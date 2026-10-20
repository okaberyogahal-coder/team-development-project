import "server-only";
import type { PrismaClient } from "../../generated/prisma/client";
import { createDatabase } from "./connection";

const databaseGlobal = globalThis as typeof globalThis & { melifeDatabase?: PrismaClient };

/** Lazy initialization keeps static builds independent of live database credentials. */
export function getDatabase(): PrismaClient {
  if (databaseGlobal.melifeDatabase) return databaseGlobal.melifeDatabase;
  const url = process.env.DATABASE_URL;
  if (!url) throw new Error("DATABASE_URL is required to access the database.");
  databaseGlobal.melifeDatabase = createDatabase(url);
  return databaseGlobal.melifeDatabase;
}
