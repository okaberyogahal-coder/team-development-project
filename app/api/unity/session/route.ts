import { getCurrentUser } from "../../../../server/auth/server";
import { authJson } from "../../../../server/auth/http";
import { AUTH_AVATARS } from "../../../../shared/auth/validation";

import {ageBadge,worldProfileReady} from '../../../../shared/profile-demographics';

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

// The cookie identifies the current user. A WebGL-provided userId is never authority.
export async function GET() {
  const headers = { Vary: "Cookie", "Cross-Origin-Resource-Policy": "same-origin" };
  try {
    const user = await getCurrentUser();
    if (!user) return authJson({ authenticated: false, profile: null }, 200, headers);
    const profile = user.profile;
    if (!worldProfileReady(profile)) return authJson({authenticated:false,profile:null,requiresProfileCompletion:true},200,headers);
    const avatarId = AUTH_AVATARS.find(avatar => avatar === profile?.avatarId) || "sprout";
    return authJson({
      authenticated: true,
      profile: {
        userId: user.id,
        nickname: profile?.nickname || "利用者",
        avatarId,
        gender: profile?.gender || null,
        ageBadge: ageBadge(profile?.age),
        teaches: profile?.teaches || [],
        wantsToLearn: profile?.wantsToLearn || [],
      },
    }, 200, headers);
  } catch {
    return authJson({ message: "ログイン状態を確認できません。しばらく待ってから再確認してください。" }, 503, headers);
  }
}
