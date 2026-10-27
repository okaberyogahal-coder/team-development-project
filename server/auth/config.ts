import { betterAuth } from "better-auth";
import { prismaAdapter } from "better-auth/adapters/prisma";
import { APIError } from "better-auth/api";
import type { Prisma, PrismaClient } from "../../generated/prisma/client";
import { MIN_PASSWORD_LENGTH } from "../../shared/auth/validation";

export function authOrigins() {
  const configured = process.env.BETTER_AUTH_URL || "http://localhost:3002";
  const url = new URL(configured);
  const local = ["localhost", "127.0.0.1", "[::1]"].includes(url.hostname);
  if (url.protocol !== "https:" && !(local && url.protocol === "http:")) {
    throw new Error("Authentication requires HTTPS outside localhost.");
  }
  const origins = [url.origin];
  if (local) {
    for (const hostname of ["localhost", "127.0.0.1"]) {
      const alias = new URL(url.origin);
      alias.hostname = hostname;
      origins.push(alias.origin);
    }
  }
  return { baseURL: url.origin, origins: [...new Set(origins)], secure: url.protocol === "https:" };
}

export function createAuth(database: PrismaClient | Prisma.TransactionClient, transaction = true) {
  const secret = process.env.BETTER_AUTH_SECRET;
  if (!secret || secret.length < 32) throw new Error("Set BETTER_AUTH_SECRET to a random value of at least 32 characters.");
  const { baseURL, origins, secure } = authOrigins();
  return betterAuth({
    appName: "ミライフ",
    baseURL,
    secret,
    trustedOrigins: origins,
    database: prismaAdapter(database, { provider: "postgresql", transaction }),
    user: { modelName: "User" },
    account: { modelName: "AuthAccount" },
    verification: { modelName: "AuthVerification" },
    emailAndPassword: {
      enabled: true,
      minPasswordLength: MIN_PASSWORD_LENGTH,
      maxPasswordLength: 128,
      autoSignIn: false,
    },
    session: {
      modelName: "AuthSession",
      expiresIn: 60 * 60 * 24 * 7,
      updateAge: 60 * 60 * 24,
      cookieCache: { enabled: false },
    },
    rateLimit: {
      enabled: true, storage: "database", modelName: "AuthRateLimit", window: 60, max: 60,
      customRules: { "/sign-in/email": { window: 60, max: 30 } },
    },
    advanced: {
      database: { generateId: "uuid" },
      cookiePrefix: "melife",
      useSecureCookies: secure,
      defaultCookieAttributes: { httpOnly: true, sameSite: "lax", path: "/" },
    },
    databaseHooks: {
      session: { create: { before: async session => {
        const user = await database.user.findUnique({ where: { id: session.userId }, select: { status: true, deletedAt: true } });
        if (!user || user.deletedAt || user.status === "SUSPENDED") {
          throw new APIError("UNAUTHORIZED", { message: "ログインできません。" });
        }
      } } },
    },
    logger: { disabled: true },
  });
}
