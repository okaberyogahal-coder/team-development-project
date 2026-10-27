import assert from "node:assert/strict";
import { randomBytes, randomUUID } from "node:crypto";
import { after, test } from "node:test";
import { createDatabase } from "../../server/db/connection";
import { createAuth } from "../../server/auth/config";
import { allowAuthAttempt, readAuthJson } from "../../server/auth/http";
import { signupInput } from "../../shared/auth/validation";

const url = new URL(process.env.DATABASE_URL || "");
assert.ok(["localhost", "127.0.0.1", "[::1]"].includes(url.hostname) && url.pathname.endsWith("_test"));
process.env.BETTER_AUTH_URL = "http://localhost:3012";
process.env.BETTER_AUTH_SECRET = randomBytes(48).toString("base64url");
const database = createDatabase(url.href);
const auth = createAuth(database);
const origin = "http://localhost:3012";
const userIds: string[] = [];
const emails: string[] = [];
const password = "correct-horse-test-12345";
const cookieHeader = (response: Response) => response.headers.getSetCookie().map(cookie => cookie.split(";")[0]).join("; ");
const post = (path: string, body: unknown, cookie?: string) => auth.handler(new Request(`${origin}/api/auth/${path}`, {
  method: "POST", headers: { origin, "Content-Type": "application/json", "x-forwarded-for": `192.0.2.${Math.floor(Math.random() * 250) + 1}`, ...(cookie ? { cookie } : {}) }, body: JSON.stringify(body),
}));

after(async () => {
  try {
    await database.user.deleteMany({ where: { id: { in: userIds } } });
    for (const email of emails) {
      // Only this suite's uniquely named rate-limit keys are removed.
      const { createHash } = await import("node:crypto");
      await database.authRateLimit.deleteMany({ where: { key: { contains: createHash("sha256").update(email).digest("hex") } } });
    }
  } finally { await database.$disconnect(); }
});

async function register() {
  const email = `${randomUUID()}@example.invalid`;
  emails.push(email);
  const result = await auth.api.signUpEmail({ body: { name: "認証検証", email, password }, asResponse: true });
  assert.equal(result.status, 200);
  const data = await result.json() as { user: { id: string }; token: null };
  userIds.push(data.user.id);
  return { email, id: data.user.id };
}

test("認証情報はハッシュで保存され、初期アカウントはPENDING・メール未確認", async () => {
  const user = await register();
  const account = await database.authAccount.findFirstOrThrow({ where: { userId: user.id } });
  assert.equal(account.providerId, "credential");
  assert.notEqual(account.password, password);
  assert.ok(account.password && account.password.length > 64);
  const row = await database.user.findUniqueOrThrow({ where: { id: user.id } });
  assert.equal(row.status, "PENDING");
  assert.equal(row.emailVerified, false);
});

test("Cookieでログインし、ログアウト後にコピー済みCookieも失効する", async () => {
  const user = await register();
  const wrong = await post("sign-in/email", { email: user.email, password: "wrong-password-123" });
  assert.equal(wrong.status, 401);
  const login = await post("sign-in/email", { email: user.email, password });
  assert.equal(login.status, 200);
  assert.ok(login.headers.getSetCookie().some(cookie => /HttpOnly/i.test(cookie) && /SameSite=Lax/i.test(cookie)));
  const cookie = cookieHeader(login);
  const headers = new Headers({ cookie });
  assert.equal((await auth.api.getSession({ headers }))?.user.id, user.id);
  assert.equal((await post("sign-out", {}, cookie)).status, 200);
  assert.equal(await auth.api.getSession({ headers }), null);
});

test("停止中・論理削除済みのアカウントはログインできない", async () => {
  const user = await register();
  await database.user.update({ where: { id: user.id }, data: { status: "SUSPENDED" } });
  assert.equal((await post("sign-in/email", { email: user.email, password })).status, 401);
  await database.user.update({ where: { id: user.id }, data: { status: "PENDING", deletedAt: new Date() } });
  assert.equal((await post("sign-in/email", { email: user.email, password })).status, 401);
});

test("外部Originからログイン・ログアウトできない", async () => {
  const response = await auth.handler(new Request(`${origin}/api/auth/sign-in/email`, {
    method: "POST", headers: { origin: "https://attacker.invalid", "Content-Type": "application/json" },
    body: JSON.stringify({ email: `${randomUUID()}@example.invalid`, password }),
  }));
  assert.equal(response.status, 403);
});

test("登録に続くプロフィール処理の失敗は認証情報もロールバックする", async () => {
  const email = `${randomUUID()}@example.invalid`;
  await assert.rejects(database.$transaction(async tx => {
    const response = await createAuth(tx, false).api.signUpEmail({ body: { name: "検証", email, password }, asResponse: true });
    assert.equal(response.status, 200);
    const { user } = await response.json() as { user: { id: string } };
    await tx.userProfile.create({ data: { userId: user.id, nickname: " " } });
  }, { timeout: 20_000 }));
  assert.equal(await database.user.count({ where: { email } }), 0);
  assert.equal(await database.authAccount.count({ where: { user: { email } } }), 0);
});

test("同時リクエストでも認証回数の上限を超えて許可しない", async () => {
  const email = `${randomUUID()}@example.invalid`;
  emails.push(email);
  const outcomes = await Promise.all(Array.from({ length: 12 }, () => allowAuthAttempt(database, "test", email, 5)));
  assert.equal(outcomes.filter(Boolean).length, 5);
});

test("登録入力は必須同意・年齢申告・配列上限をサーバーで検証する", () => {
  const input = { email: " TEST@example.invalid ", password, confirmation: password, nickname: "利用者", avatar: "cat", adult: true, terms: true, privacy: true, interests: [], teaches: [], wantsToLearn: [], strengths: "", goal: "" };
  assert.equal(signupInput(input).email, "test@example.invalid");
  for (const bad of [{ adult: false }, { terms: false }, { privacy: false }, { avatar: "unknown" }, { interests: Array(11).fill("写真") }, { password: "short" }, { nickname: " " }]) {
    assert.throws(() => signupInput({ ...input, ...bad }));
  }
});

test("JSON以外・過大な登録リクエストを拒否する", async () => {
  await assert.rejects(readAuthJson(new Request(origin, { method: "POST", body: "plain-text" })));
  await assert.rejects(readAuthJson(new Request(origin, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ value: "a".repeat(17_000) }) })));
});
