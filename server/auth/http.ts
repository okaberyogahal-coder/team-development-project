import { createHash } from "node:crypto";
import type { PrismaClient } from "../../generated/prisma/client";
import { authOrigins } from "./config";

export function authJson(data: unknown, status = 200, headers?: HeadersInit) {
  const responseHeaders = new Headers(headers);
  responseHeaders.set("Cache-Control", "no-store");
  return Response.json(data, { status, headers: responseHeaders });
}

export function checkOrigin(request: Request) {
  const origin = request.headers.get("origin");
  return origin !== null && authOrigins().origins.includes(origin);
}

export async function readAuthJson(request: Request): Promise<unknown> {
  if (request.headers.get("content-type")?.split(";")[0].trim().toLowerCase() !== "application/json") throw new Error("INVALID_BODY");
  const reader = request.body?.getReader();
  if (!reader) throw new Error("INVALID_BODY");
  const chunks: Uint8Array[] = [];
  let size = 0;
  while (true) {
    const { done, value } = await reader.read();
    if (done) break;
    size += value.length;
    if (size > 16_384) { await reader.cancel(); throw new Error("INVALID_BODY"); }
    chunks.push(value);
  }
  return JSON.parse(Buffer.concat(chunks).toString("utf8"));
}

/** Atomic, shared across Node processes; keys do not store raw emails. */
export async function allowAuthAttempt(database: PrismaClient, action: string, email: string, limit: number, windowSeconds = 60) {
  const key = `account:${action}:${createHash("sha256").update(email).digest("hex")}`;
  const now = Date.now();
  const rows = await database.$queryRaw<{ count: number }[]>`
    INSERT INTO app.auth_rate_limits (id, key, count, last_request)
    VALUES (gen_random_uuid(), ${key}, 1, ${BigInt(now)})
    ON CONFLICT (key) DO UPDATE SET
      count = CASE WHEN app.auth_rate_limits.last_request < ${BigInt(now - windowSeconds * 1000)} THEN 1 ELSE app.auth_rate_limits.count + 1 END,
      last_request = CASE WHEN app.auth_rate_limits.last_request < ${BigInt(now - windowSeconds * 1000)} THEN ${BigInt(now)} ELSE app.auth_rate_limits.last_request END
    RETURNING count
  `;
  return rows[0].count <= limit;
}
