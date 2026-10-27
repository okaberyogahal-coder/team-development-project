import { getAuth } from "../../../../server/auth/server";
import { getDatabase } from "../../../../server/db/client";
import { allowAuthAttempt, authJson, checkOrigin, readAuthJson } from "../../../../server/auth/http";
import { normalizeEmail } from "../../../../shared/auth/validation";

export const runtime = "nodejs";

async function handler(request: Request) {
  const path = new URL(request.url).pathname;
  const signIn = path === "/api/auth/sign-in/email" && request.method === "POST";
  const signOut = path === "/api/auth/sign-out" && request.method === "POST";
  const getSession = path === "/api/auth/get-session" && request.method === "GET";
  // Sign-up must pass the atomic profile/consent workflow; other account APIs are not exposed.
  if (!signIn && !signOut && !getSession) return authJson({ message: "この操作は利用できません。" }, 404);
  if (request.method === "POST" && !checkOrigin(request)) return authJson({ message: "この画面からもう一度操作してください。" }, 403);
  try {
    let forwarded = request;
    if (signIn) {
      let email: string;
      let password: string;
      try {
        const body = await readAuthJson(request) as Record<string, unknown>;
        email = normalizeEmail(body?.email);
        if (typeof body.password !== "string" || !body.password || body.password.length > 128) throw new Error();
        password = body.password;
      } catch { return authJson({ message: "メールアドレスまたはパスワードを確認してください。" }, 400); }
      if (!await allowAuthAttempt(getDatabase(), "signin", email, 10)) {
        return authJson({ message: "しばらく待ってから、もう一度お試しください。" }, 429, { "Retry-After": "60" });
      }
      const headers = new Headers(request.headers);
      headers.delete("content-length");
      forwarded = new Request(request.url, { method: "POST", headers, body: JSON.stringify({ email, password }) });
    }
    const result = await getAuth().handler(forwarded);
    if (signIn) {
      const message = result.status === 429 ? "しばらく待ってから、もう一度お試しください。" : result.ok ? "ログインしました。" : "メールアドレスまたはパスワードを確認してください。";
      const response = authJson({ message }, result.status);
      if (result.headers.has("retry-after")) response.headers.set("Retry-After", result.headers.get("retry-after")!);
      for (const cookie of result.headers.getSetCookie()) response.headers.append("Set-Cookie", cookie);
      return response;
    }
    // Do not expose the underlying bearer token/session object to browser JavaScript.
    if (getSession) {
      const session = result.ok ? await result.json() as { user?: { id: string } } | null : null;
      const user = session?.user ? await getDatabase().user.findUnique({ where: { id: session.user.id }, select: { deletedAt: true, status: true } }) : null;
      return authJson({ authenticated: Boolean(user && !user.deletedAt && user.status !== "SUSPENDED") });
    }
    result.headers.set("Cache-Control", "no-store");
    return result;
  } catch { return authJson({ message: "認証サービスに接続できません。しばらく待ってからお試しください。" }, 503); }
}

export const GET = handler;
export const POST = handler;
