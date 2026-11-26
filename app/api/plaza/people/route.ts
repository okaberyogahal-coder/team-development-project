import {worldProfileReady} from '../../../../shared/profile-demographics';
import {authJson, checkOrigin} from '../../../../server/auth/http';
import {getCurrentUserWithSession} from '../../../../server/auth/server';
import {getDatabase} from '../../../../server/db/client';
import {refreshPlazaPeople} from '../../../../server/plaza/people';

export const runtime = 'nodejs';

export async function POST(request: Request) {
  if (!checkOrigin(request)) return authJson({message: 'この画面からもう一度操作してください。'}, 403);
  try {
    const account = await getCurrentUserWithSession();
    if (!account) return authJson({message: 'ログインしてください。'}, 401);
    const {user, sessionId} = account;
    if (request.headers.get('x-profile-owner') !== user.id) {
      return authJson({message: 'ログイン中のアカウントが切り替わりました。画面を開き直してください。'}, 409);
    }
    if (!worldProfileReady(user.profile)) return authJson({message: '先にプロフィールを設定してください。'}, 403);
    const people = await refreshPlazaPeople(getDatabase(), user.id, sessionId);
    return authJson({people}, 200, {Vary: 'Cookie', 'Cross-Origin-Resource-Policy': 'same-origin'});
  } catch {
    return authJson({message: '広場にいる人を読み込めませんでした。もう一度お試しください。'}, 503);
  }
}
