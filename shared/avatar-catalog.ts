import { AVATAR_OPTIONS, BASE_AVATARS, restoreAvatarChoice, type AvatarChoice } from './avatar-types';

export interface AvatarCatalog {
  models: { id: string; defaults: AvatarChoice }[];
  partCounts: Record<string, number>;
  skinColorSupported: boolean;
}

const isRecord = (value: unknown): value is Record<string, unknown> =>
  value !== null && typeof value === 'object' && !Array.isArray(value);

// Catalogs describe capabilities of the loaded build, never approval of source assets.
export function readAvatarCatalog(value: unknown): AvatarCatalog | null {
  if (!isRecord(value) || !Array.isArray(value.models) || !Array.isArray(value.slots)) return null;
  const partCounts: Record<string, number> = {};
  for (const slot of value.slots) {
    if (!isRecord(slot) || typeof slot.key !== 'string' || typeof slot.count !== 'number') continue;
    if (slot.key !== 'body' && slot.key !== 'costume' && !Object.hasOwn(AVATAR_OPTIONS, slot.key)) continue;
    if (!Number.isSafeInteger(slot.count) || slot.count < 0) continue;
    // Conflicting duplicate capabilities cannot safely enable a part.
    partCounts[slot.key] = Math.min(partCounts[slot.key] ?? slot.count, slot.count);
  }
  const models: AvatarCatalog['models'] = [];
  for (const model of value.models) {
    if (!isRecord(model) || typeof model.id !== 'string' || !BASE_AVATARS.some(base => base.id === model.id)) continue;
    const restored = restoreAvatarChoice(model.defaults);
    if (!restored || restored.adjusted || restored.choice.baseAvatarId !== model.id) continue;
    const choice = restored.choice;
    const supported = (['body', 'face', 'hair', 'outerwear', 'pants', 'shoes', 'glasses', 'accessory'] as const)
      .every(key => choice[key] === -1 || choice[key] < (partCounts[key] ?? 0));
    if (supported && !models.some(entry => entry.id === model.id)) models.push({ id: model.id, defaults: choice });
  }
  return { models, partCounts, skinColorSupported: value.skinColorSupported === true };
}
