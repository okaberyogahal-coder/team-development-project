import { getDatabase } from "../../../../server/db/client";
import { createAuth } from "../../../../server/auth/config";
import { getAuth } from "../../../../server/auth/server";
import { allowAuthAttempt, authJson, checkOrigin, readAuthJson } from "../../../../server/auth/http";
import { AuthInputError, CONSENT_VERSION, signupInput } from "../../../../shared/auth/validation";

export const runtime = "nodejs";

export async function POST(request: Request) {
  if (!checkOrigin(request)) return authJson({ message: "この画面からもう一度操作してください。" }, 403);
  let input: ReturnType<typeof signupInput>;
  try { input = signupInput(await readAuthJson(request)); }
  catch (error) { return authJson({ message: error instanceof AuthInputError ? error.message : "入力内容を確認してください。" }, 400); }
  try {
    const database = getDatabase();
    if (!await allowAuthAttempt(database, "signup-service", "global", 30)) {
      return authJson({ message: "しばらく待ってから、もう一度お試しください。" }, 429, { "Retry-After": "60" });
    }
    if (!await allowAuthAttempt(database, "signup", input.email, 5, 600)) {
      return authJson({ message: "しばらく待ってから、もう一度お試しください。" }, 429, { "Retry-After": "600" });
    }
    // Credentials, profile, preferences and consent are committed together.
    await database.$transaction(async tx => {
      const result = await createAuth(tx, false).api.signUpEmail({
        body: { email: input.email, password: input.password, name: input.nickname },
        headers: request.headers,
        asResponse: true,
      });
      if (!result.ok) throw new Error("REGISTRATION_REJECTED");
      const created = await result.json() as { user: { id: string } };
      const userId = created.user.id;
      await tx.userProfile.create({ data: {
        userId, nickname: input.nickname, avatarId: input.avatar,
        teaches: input.teaches, wantsToLearn: input.wantsToLearn,
      } });
      await tx.learningPreferences.create({ data: { userId, interests: input.interests, strengths: input.strengths, goal: input.goal } });
      await tx.policyConsent.createMany({ data: ["development-use", "account-data-storage", "adult-self-declaration"].map(policy => ({ userId, policy, version: CONSENT_VERSION })) });
    }, { timeout: 20_000 });
    // Only issue a login cookie after the whole registration transaction commits.
    try {
      const signIn = await getAuth().api.signInEmail({ body: { email: input.email, password: input.password }, headers: request.headers, asResponse: true });
      const response = authJson({ created: true, signedIn: signIn.ok });
      for (const cookie of signIn.headers.getSetCookie()) response.headers.append("Set-Cookie", cookie);
      return response;
    } catch {
      // The account already exists: offer login instead of reporting a failed registration.
      return authJson({ created: true, signedIn: false });
    }
  } catch {
    return authJson({ message: "登録できませんでした。入力内容を確認するか、ログインしてください。" }, 400);
  }
}
