import { authJson, checkOrigin, readAuthJson } from "../auth/http";
import { FriendActionError } from "./errors";

type User = { id: string };
type FriendPage = { friends: unknown[]; incoming: unknown[]; outgoing: unknown[] };
type Dependencies = {
  getUser: () => Promise<User | null>;
  read: (userId: string) => Promise<FriendPage>;
  search: (userId: string, nickname: string) => Promise<unknown[]>;
  request: (userId: string, targetId: string) => Promise<void>;
  respond: (userId: string, requesterId: string, response: "accept" | "decline") => Promise<void>;
  remove: (userId: string, targetId: string) => Promise<void>;
  allow: (userId: string, action: "search" | "request") => Promise<boolean>;
};

const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

async function readTargetId(request: Request) {
  const body: unknown = await readAuthJson(request);
  if (!body || typeof body !== "object" || !("targetId" in body) || typeof body.targetId !== "string" || !uuid.test(body.targetId)) {
    throw new Error("INVALID_INPUT");
  }
  return body.targetId.toLowerCase();
}

function actionResponse(error: unknown) {
  if (error instanceof FriendActionError) {
    if (error.code === "TARGET_UNAVAILABLE") return authJson({ message: "利用者が見つかりませんでした。" }, 404);
    if (error.code === "CONNECTION_EXISTS") return authJson({ message: "すでにフレンド、または申請中です。" }, 409);
    if (error.code === "REQUEST_NOT_FOUND") return authJson({ message: "受信した申請が見つかりません。画面を更新してください。" }, 409);
    if (error.code === "NOT_FRIENDS") return authJson({ message: "対象のフレンドまたは申請が見つかりません。" }, 409);
  }
  return authJson({ message: "処理できませんでした。時間をおいてもう一度お試しください。" }, 503);
}

export function createFriendHandlers(deps: Dependencies) {
  return {
    async GET(request: Request) {
      try {
        const user = await deps.getUser();
        if (!user) return authJson({ message: "ログインしてください。" }, 401);
        const nickname = new URL(request.url).searchParams.get("nickname")?.trim() ?? "";
        if (nickname.length > 20 || /[\u0000-\u001f\u007f]/.test(nickname)) {
          return authJson({ message: "ニックネームは1〜20文字で入力してください。" }, 400);
        }
        if (nickname && !await deps.allow(user.id, "search")) {
          return authJson({ message: "検索回数が上限に達しました。少し待ってからお試しください。" }, 429, { "Retry-After": "60" });
        }
        const data = await deps.read(user.id);
        const matches = nickname ? await deps.search(user.id, nickname) : [];
        return authJson({ ...data, matches }, 200, { Vary: "Cookie", "Cross-Origin-Resource-Policy": "same-origin" });
      } catch {
        return authJson({ message: "フレンド情報を読み込めませんでした。時間をおいてもう一度お試しください。" }, 503);
      }
    },
    async POST(request: Request) {
      if (!checkOrigin(request)) return authJson({ message: "この画面からもう一度操作してください。" }, 403);
      try {
        const user = await deps.getUser();
        if (!user) return authJson({ message: "ログインしてください。" }, 401);
        let targetId: string;
        try {
          targetId = await readTargetId(request);
        } catch {
          return authJson({ message: "申請する利用者を選び直してください。" }, 400);
        }
        if (targetId === user.id) return authJson({ message: "自分自身には申請できません。" }, 400);
        if (!await deps.allow(user.id, "request")) {
          return authJson({ message: "操作回数が上限に達しました。少し待ってからお試しください。" }, 429, { "Retry-After": "60" });
        }
        await deps.request(user.id, targetId);
        return authJson({ message: "フレンド申請を送りました。" });
      } catch (error) {
        return actionResponse(error);
      }
    },
    async PATCH(request: Request) {
      if (!checkOrigin(request)) return authJson({ message: "この画面からもう一度操作してください。" }, 403);
      try {
        const user = await deps.getUser();
        if (!user) return authJson({ message: "ログインしてください。" }, 401);
        let body: unknown;
        try {
          body = await readAuthJson(request);
        } catch {
          return authJson({ message: "申請内容を確認してください。" }, 400);
        }
        if (!body || typeof body !== "object" || !("targetId" in body) || typeof body.targetId !== "string" || !uuid.test(body.targetId) ||
            !("action" in body) || (body.action !== "accept" && body.action !== "decline")) {
          return authJson({ message: "申請内容を確認してください。" }, 400);
        }
        const targetId = body.targetId.toLowerCase();
        if (targetId === user.id) return authJson({ message: "申請内容を確認してください。" }, 400);
        await deps.respond(user.id, targetId, body.action);
        return authJson({ message: body.action === "accept" ? "フレンドになりました。" : "申請を見送りました。" });
      } catch (error) {
        return actionResponse(error);
      }
    },
    async DELETE(request: Request) {
      if (!checkOrigin(request)) return authJson({ message: "この画面からもう一度操作してください。" }, 403);
      try {
        const user = await deps.getUser();
        if (!user) return authJson({ message: "ログインしてください。" }, 401);
        let targetId: string;
        try {
          targetId = await readTargetId(request);
        } catch {
          return authJson({ message: "対象を選び直してください。" }, 400);
        }
        if (targetId === user.id) return authJson({ message: "対象を選び直してください。" }, 400);
        await deps.remove(user.id, targetId);
        return authJson({ message: "フレンドまたは申請を削除しました。" });
      } catch (error) {
        return actionResponse(error);
      }
    },
  };
}
