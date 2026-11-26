import {validAppearance, type AvatarAppearance} from './demo-profile';

export type AccountAvatar = {
  ownerId: string;
  appearance: AvatarAppearance | null;
  version: number;
};

export function avatarSaveInput(value: unknown): {appearance: AvatarAppearance; version: number} {
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw new Error('外見の設定を確認してください。');
  const input = value as Record<string, unknown>;
  if (Object.keys(input).some(key => key !== 'appearance' && key !== 'version')) throw new Error('外見の設定を確認してください。');
  if (!validAppearance(input.appearance) || !Number.isSafeInteger(input.version) || (input.version as number) < 0) {
    throw new Error('外見の設定を確認してください。');
  }
  const appearance = input.appearance;
  if (appearance.version !== 3) {
    return {appearance: {version: appearance.version, baseVariant: appearance.baseVariant, parts: appearance.parts, colors: appearance.colors, ...(appearance.version === 2 ? {costume: appearance.costume} : {})}, version: input.version as number};
  }
  return {appearance, version: input.version as number};
}

export async function saveAvatar(ownerId: string, appearance: AvatarAppearance, version: number): Promise<AccountAvatar> {
  let response: Response;
  try {
    response = await fetch('/api/account/avatar', {
      method: 'PUT',
      credentials: 'same-origin',
      headers: {'Content-Type': 'application/json', 'X-Profile-Owner': ownerId},
      body: JSON.stringify({appearance, version}),
      signal: AbortSignal.timeout(15000),
    });
  } catch {
    throw new Error('保存結果を確認できませんでした。編集内容は残っています。通信状態を確認して、もう一度お試しください。');
  }
  const data = await response.json();
  if (!response.ok) throw new Error(data.message || '保存できませんでした。もう一度お試しください。');
  if (data.ownerId !== ownerId || !validAppearance(data.appearance) || !Number.isSafeInteger(data.version)) {
    throw new Error('保存結果を確認できませんでした。マイページで確認してください。');
  }
  try {
    const channel = new BroadcastChannel('melife-avatar');
    channel.postMessage({ownerId, version:data.version});
    channel.close();
  } catch { /* The world also refreshes its session on focus and periodically. */ }
  return data;
}
