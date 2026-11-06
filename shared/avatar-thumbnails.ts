import { BASE_AVATARS } from './avatar-types';

// Only categories containing a person depend on the selected base model.
// Shared isolated garments retain their existing part number and image URL.
export function avatarPartThumbnail(slot: string, value: number, baseAvatarId: string, headImageRoot?: string): string {
  if (headImageRoot && ['hair', 'face'].includes(slot) && BASE_AVATARS.some(base => base.id === baseAvatarId)) {
    const ext = headImageRoot.includes('v3-unified') ? 'png' : 'webp';
    return `${headImageRoot}/${baseAvatarId}/${slot}/${value}.${ext}`;
  }
  const hasPerson = ['hair', 'face', 'glasses', 'accessory', 'costume'].includes(slot);
  if (hasPerson && BASE_AVATARS.some(base => base.id === baseAvatarId)) {
    return `/images/avatar-parts/by-avatar/${baseAvatarId}/${slot}/${value}.webp`;
  }
  return `/images/avatar-parts/${slot}/${value}.webp`;
}
