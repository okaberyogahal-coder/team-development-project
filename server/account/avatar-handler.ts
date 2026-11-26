import {avatarSaveInput} from '../../shared/account-avatar-save';
import type {AvatarAppearance} from '../../shared/demo-profile';
import {authJson, checkOrigin, readAuthJson} from '../auth/http';
import {ProfileAccessError, ProfileConflictError} from './profile-handler';

type Dependencies = {
  getUser: () => Promise<{id: string} | null>;
  read: (id: string) => Promise<unknown>;
  save: (id: string, appearance: AvatarAppearance, version: number) => Promise<unknown>;
  allowSave: (id: string) => Promise<boolean>;
};

export function createAvatarHandlers(deps: Dependencies) {
  return {
    async GET() {
      try {
        const user = await deps.getUser();
        if (!user) return authJson({message: 'ログインしてください。'}, 401);
        return authJson(await deps.read(user.id), 200, {Vary: 'Cookie', 'Cross-Origin-Resource-Policy': 'same-origin'});
      } catch {
        return authJson({message: 'アバターを読み込めませんでした。もう一度お試しください。'}, 503);
      }
    },
    async PUT(request: Request) {
      if (!checkOrigin(request)) return authJson({message: 'この画面からもう一度操作してください。'}, 403);
      try {
        const user = await deps.getUser();
        if (!user) return authJson({message: 'ログインしてください。'}, 401);
        if (request.headers.get('x-profile-owner') !== user.id) {
          return authJson({message: 'ログイン中の利用者が変わりました。最新の画面を開いて保存してください。'}, 409);
        }
        let input: ReturnType<typeof avatarSaveInput>;
        try { input = avatarSaveInput(await readAuthJson(request)); }
        catch { return authJson({message: '外見の設定を確認してください。'}, 400); }
        if (!await deps.allowSave(user.id)) return authJson({message: '少し待ってから保存してください。'}, 429, {'Retry-After': '60'});
        return authJson(await deps.save(user.id, input.appearance, input.version));
      } catch (error) {
        if (error instanceof ProfileAccessError) return authJson({message: 'ログイン状態を確認してください。'}, 401);
        if (error instanceof ProfileConflictError) return authJson({message: '別の画面で外見が保存されています。この画面の編集内容は残っています。最新の外見を別タブで確認してください。'}, 409);
        return authJson({message: '保存できませんでした。編集内容は残っています。もう一度お試しください。'}, 503);
      }
    },
  };
}
