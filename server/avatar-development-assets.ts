import 'server-only';
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { LEGACY_AVATAR_ASSETS, resolveAvatarDevelopmentAssets } from '../shared/avatar-development-assets';

function hasCompleteV3WorldBuild(worldPath: string, releasePath: string): boolean {
  const release: unknown = JSON.parse(readFileSync(releasePath, 'utf8'));
  if (!release || typeof release !== 'object' || !('profile' in release) ||
      release.profile !== 'v3-unified-20261104' || !('result' in release) || release.result !== 'Succeeded') {
    return false;
  }

  const worldDirectory = dirname(join(process.cwd(), 'public', worldPath.replace(/^\/+/, '')));
  const buildDirectory = join(worldDirectory, 'Build');
  if (!existsSync(join(worldDirectory, 'index.html')) || !existsSync(buildDirectory)) return false;

  const files = readdirSync(buildDirectory);
  const loader = files.find((file) => file.endsWith('.loader.js'));
  if (!loader) return false;
  const buildName = loader.slice(0, -'.loader.js'.length);
  return ['.data', '.framework.js', '.wasm'].every((extension) => files.includes(`${buildName}${extension}`));
}

export function getAvatarDevelopmentAssets(requestedProfile?: string) {
  const assets = resolveAvatarDevelopmentAssets(
    process.env.NODE_ENV,
    requestedProfile ?? process.env.MIRAIFU_AVATAR_ASSET_PROFILE,
  );
  if (assets.id !== 'legacy') {
    const worldPath = assets.worldSrc.split('?')[0];
    const releasePath = join(process.cwd(), 'public', worldPath.replace(/\/index\.html$/, '/release.json').replace(/^\/+/, ''));
    if (!existsSync(releasePath)) return LEGACY_AVATAR_ASSETS;
    if (assets.id === 'v3-unified-20261104' && !hasCompleteV3WorldBuild(worldPath, releasePath)) {
      return LEGACY_AVATAR_ASSETS;
    }
  }
  return assets;
}
