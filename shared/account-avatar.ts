import {readDemoProfile, type DemoProfile} from './demo-profile';
import type {AccountAvatar} from './account-avatar-save';

/** Resolve display data only; opening the account page never writes a wardrobe. */
export function resolveAccountAvatar(storage: Pick<Storage,'getItem'>, ownerId: string, nickname?: string, accountAvatar?: AccountAvatar | null): DemoProfile | null {
 if (accountAvatar?.ownerId === ownerId) {
  if (accountAvatar.appearance) return {version:1,id:ownerId,nickname:nickname ?? '利用者',appearance:accountAvatar.appearance};
  if (accountAvatar.version > 0) return null;
 }
 const saved = readDemoProfile(storage);
 if (!saved || saved.id !== ownerId || !saved.appearance) return null;
 return {...saved,nickname:nickname ?? saved.nickname};
}
