export const MIN_PASSWORD_LENGTH = 12;
export const CONSENT_VERSION = "development-2026-10-20";
export const AUTH_AVATARS = ["sprout", "cat", "bear", "rabbit", "penguin", "fox"] as const;

export class AuthInputError extends Error {}

export function normalizeEmail(value: unknown): string {
  if (typeof value !== "string") throw new AuthInputError("メールアドレスを確認してください。");
  const email = value.trim().toLowerCase();
  if (email.length > 254 || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
    throw new AuthInputError("メールアドレスを確認してください。");
  }
  return email;
}

function text(value: unknown, max: number, required = false): string {
  if (typeof value !== "string" || value.trim().length > max || (required && !value.trim())) {
    throw new AuthInputError("入力項目の文字数・内容を確認してください。");
  }
  return value.trim();
}

function tags(value: unknown): string[] {
  if (!Array.isArray(value) || value.length > 10) throw new AuthInputError("各分野は10個以内で選んでください。");
  return [...new Set(value.map(item => text(item, 30, true)))];
}

export function signupInput(value: unknown) {
  if (!value || typeof value !== "object") throw new AuthInputError("入力内容を確認してください。");
  const data = value as Record<string, unknown>;
  const email = normalizeEmail(data.email);
  if (typeof data.password !== "string" || data.password.length < MIN_PASSWORD_LENGTH || data.password.length > 128) {
    throw new AuthInputError(`パスワードは${MIN_PASSWORD_LENGTH}〜128文字で入力してください。`);
  }
  if (data.password !== data.confirmation) throw new AuthInputError("パスワードが一致しません。");
  if (data.adult !== true) throw new AuthInputError("現在の開発版は18歳以上の方が登録できます。");
  if (data.terms !== true || data.privacy !== true) throw new AuthInputError("開発版の利用条件と情報の保存を確認してください。");
  if (typeof data.avatar !== "string" || !AUTH_AVATARS.includes(data.avatar as typeof AUTH_AVATARS[number])) {
    throw new AuthInputError("アバターを選び直してください。");
  }
  return {
    email, password: data.password, nickname: text(data.nickname, 20, true), avatar: data.avatar,
    interests: tags(data.interests), teaches: tags(data.teaches), wantsToLearn: tags(data.wantsToLearn),
    strengths: text(data.strengths, 500), goal: text(data.goal, 500),
  };
}
