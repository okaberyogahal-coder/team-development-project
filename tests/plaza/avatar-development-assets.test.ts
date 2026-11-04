import test from 'node:test';
import assert from 'node:assert/strict';
import {
  resolveAvatarDevelopmentAssets,
  LEGACY_AVATAR_ASSETS,
  V3_UNIFIED_ASSETS,
} from '../../shared/avatar-development-assets';

test('the explicit development profile is never activated in production or test', () => {
  for (const environment of ['production', 'test', undefined]) {
    assert.equal(resolveAvatarDevelopmentAssets(environment, 'face-seed-20261103'), LEGACY_AVATAR_ASSETS);
  }
});
test('development defaults to the new assets and can roll back without changing saved choice', () => {
  const fresh = resolveAvatarDevelopmentAssets('development');
  assert.equal(fresh.id, 'face-seed-20261103');
  assert.equal(new URL(fresh.worldSrc, 'http://localhost').searchParams.get('mode'), 'world');
  assert.equal(resolveAvatarDevelopmentAssets('development', 'legacy'), LEGACY_AVATAR_ASSETS);
  assert.equal(resolveAvatarDevelopmentAssets('development', 'unknown'), LEGACY_AVATAR_ASSETS);
});

test('the v3 editor uses the shared world build and can explicitly select it from the plaza route', () => {
  assert.equal(V3_UNIFIED_ASSETS.previewSrc, '/unity/AvatarV3Unified20261104/index.html?mode=preview');
  assert.equal(V3_UNIFIED_ASSETS.worldSrc, '/unity/AvatarV3World20261104/index.html?mode=world');
  assert.equal(resolveAvatarDevelopmentAssets('development', 'face-seed-20261103').worldSrc, '/unity/AvatarFaceSeed20261103/index.html?mode=world');
  assert.equal(resolveAvatarDevelopmentAssets('development', V3_UNIFIED_ASSETS.id), V3_UNIFIED_ASSETS);
  assert.equal(resolveAvatarDevelopmentAssets('production', V3_UNIFIED_ASSETS.id), V3_UNIFIED_ASSETS);
});

test('new head images follow the selected base ID and keep other categories on existing assets', async () => {
  const { avatarPartThumbnail } = await import('../../shared/avatar-thumbnails');
  const profile = resolveAvatarDevelopmentAssets('development');
  for (const id of ['young-man', 'young-woman', 'middle-aged-man', 'middle-aged-woman', 'elderly-man', 'elderly-woman']) {
    for (const slot of ['hair', 'face']) for (const value of [0, 1, 2]) {
      const image = avatarPartThumbnail(slot, value, id, profile.headImageRoot);
      assert.equal(image, `${profile.headImageRoot}/${id}/${slot}/${value}.webp`);
    }
    for (const slot of ['outerwear', 'pants', 'shoes', 'glasses', 'accessory', 'costume']) {
      assert.equal(avatarPartThumbnail(slot, 0, id, profile.headImageRoot), avatarPartThumbnail(slot, 0, id));
    }
  }
});
