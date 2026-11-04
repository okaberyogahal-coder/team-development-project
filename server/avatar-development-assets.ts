import 'server-only';
import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { LEGACY_AVATAR_ASSETS, resolveAvatarDevelopmentAssets } from '../shared/avatar-development-assets';

export function getAvatarDevelopmentAssets(requestedProfile?: string) {
  const assets = resolveAvatarDevelopmentAssets(
    process.env.NODE_ENV,
    requestedProfile ?? process.env.MIRAIFU_AVATAR_ASSET_PROFILE,
  );
  if (assets.id !== 'legacy') {
    const worldPath = assets.worldSrc.split('?')[0].replace(/\/index\.html$/, '/release.json');
    const releasePath = join(process.cwd(), 'public', worldPath.replace(/^\/+/, ''));
    if (!existsSync(releasePath)) return LEGACY_AVATAR_ASSETS;
  }
  return assets;
}
