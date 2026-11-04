export interface AvatarAssetProfile {
  id: 'legacy' | 'face-seed-20261103' | 'v3-unified-20261104';
  previewSrc: string;
  worldSrc: string;
  baseImageRoot: string;
  headImageRoot?: string;
}

export const LEGACY_AVATAR_ASSETS: AvatarAssetProfile = {
  id: 'legacy', previewSrc: '/unity/AvatarPreview/index.html',
  worldSrc: '/unity/FutureCity/index.html', baseImageRoot: '/images/avatars',
};
  export const FACE_SEED_ASSETS: AvatarAssetProfile = {
    id: 'face-seed-20261103',
    previewSrc: '/unity/AvatarFaceSeed20261103/index.html?mode=preview',
    worldSrc: '/unity/AvatarFaceSeed20261103/index.html?mode=world',
    baseImageRoot: '/images/avatar-face-seed-20261103/base',
    headImageRoot: '/images/avatar-face-seed-20261103/heads',
  };
  export const V3_UNIFIED_ASSETS: AvatarAssetProfile = {
    id: 'v3-unified-20261104',
    previewSrc: '/unity/AvatarV3Unified20261104/index.html?mode=preview',
    worldSrc: '/unity/AvatarV3World20261104/index.html?mode=world',
    baseImageRoot: '/images/avatar-v3-unified-20261104/base',
    headImageRoot: '/images/avatar-v3-unified-20261104/heads',
  };

  // Server pages select the profile; production always retains the existing build.
  export function resolveAvatarDevelopmentAssets(environment: string | undefined, requested ?: string): AvatarAssetProfile {
    if (requested === 'v3-unified-20261104') return V3_UNIFIED_ASSETS;
return environment === 'development' && (requested === undefined || requested === 'face-seed-20261103')
  ? FACE_SEED_ASSETS : LEGACY_AVATAR_ASSETS;
}
