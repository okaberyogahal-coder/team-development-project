import { loadEnvConfig } from "@next/env";

loadEnvConfig(process.cwd());

export function authTestDatabaseUrl() {
  const url = new URL(process.env.DATABASE_TEST_URL || "");
  const hosts = ["localhost", "127.0.0.1", "[::1]"];
  if (!hosts.includes(url.hostname) || !["postgres:", "postgresql:"].includes(url.protocol) || !decodeURIComponent(url.pathname).endsWith("_test")) {
    throw new Error("Auth browser tests require an isolated local DATABASE_TEST_URL ending in _test.");
  }
  if (process.env.DATABASE_URL) {
    const other = new URL(process.env.DATABASE_URL);
    if (hosts.includes(other.hostname) && (other.port || "5432") === (url.port || "5432") && decodeURIComponent(other.pathname) === decodeURIComponent(url.pathname)) {
      throw new Error("Auth browser tests must not use the development database.");
    }
  }
  return url.href;
}
