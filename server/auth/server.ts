import "server-only";
import { headers } from "next/headers";
import { getDatabase } from "../db/client";
import { createAuth } from "./config";

export function getAuth() { return createAuth(getDatabase()); }

export async function getCurrentUser() {
  const requestHeaders = await headers();
  // Public pages remain available before the local database/auth environment is set up.
  if (!/(?:^|;\s*)(?:__Secure-)?melife\.session_token=/.test(requestHeaders.get("cookie") || "")) return null;
  const session = await getAuth().api.getSession({ headers: requestHeaders });
  if (!session) return null;
  const user = await getDatabase().user.findUnique({
    where: { id: session.user.id },
    select: { id: true, email: true, status: true, deletedAt: true, profile: true, preferences: true },
  });
  if (!user || user.deletedAt || user.status === "SUSPENDED") return null;
  return user;
}
