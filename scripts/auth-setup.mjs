import { randomBytes } from "node:crypto";
import { existsSync, readFileSync, writeFileSync } from "node:fs";

const path = ".env.local";
let current = existsSync(path) ? readFileSync(path, "utf8") : "";
for (const [key, generate] of [
  ["BETTER_AUTH_SECRET", () => randomBytes(48).toString("base64url")],
  ["BETTER_AUTH_URL", () => "http://localhost:3002"],
]) {
  const pattern = new RegExp(`^${key}=(.*)$`, "m");
  const match = current.match(pattern);
  const existing = match?.[1].trim();
  if (existing && existing !== '\"\"' && existing !== "''") continue;
  const line = `${key}=${generate()}`;
  current = match ? current.replace(pattern, line) : `${current}\n${line}\n`;
}
writeFileSync(path, current, "utf8");
console.log("Local auth environment is ready. Existing values were preserved; no secrets are displayed.");
