import { loadEnvConfig } from "@next/env";
import { spawnSync } from "node:child_process";
import path from "node:path";
import { readdirSync } from "node:fs";

loadEnvConfig(process.cwd());
const target = process.env.DATABASE_TEST_URL;
if (!target) throw new Error("DATABASE_TEST_URL is required. Use an isolated local PostgreSQL database ending in _test.");
const url = new URL(target);
const loopbackHosts = ["localhost", "127.0.0.1", "[::1]"];
if (!["postgres:", "postgresql:"].includes(url.protocol)
    || !loopbackHosts.includes(url.hostname)
    || !url.pathname.endsWith("_test")) {
  throw new Error("Database tests require a localhost PostgreSQL database whose name ends in _test.");
}
if (process.env.DATABASE_URL) {
  const development = new URL(process.env.DATABASE_URL);
  const sameHost = development.hostname === url.hostname
    || (loopbackHosts.includes(development.hostname) && loopbackHosts.includes(url.hostname));
  if (sameHost && (development.port || "5432") === (url.port || "5432")
      && decodeURIComponent(development.pathname) === decodeURIComponent(url.pathname)) {
    throw new Error("The test database must be different from DATABASE_URL.");
  }
}

const env = { ...process.env, DATABASE_URL: target, DIRECT_URL: target };
const migrate = spawnSync(process.execPath, [path.resolve("node_modules/prisma/build/index.js"), "migrate", "deploy"], { env, stdio: "inherit" });
if (migrate.status !== 0) process.exit(migrate.status || 1);
const files = readdirSync("tests/database").filter(file => file.endsWith(".test.ts")).map(file => path.join("tests/database", file));
const result = spawnSync(process.execPath, ["--import", "tsx", "--test", ...files], { env, stdio: "inherit" });
process.exit(result.status || (result.error ? 1 : 0));
