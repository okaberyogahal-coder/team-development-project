/** Browser-only walkthrough data. Never store email, passwords or auth tokens here. */
export const DEMO_PROFILE_KEY = 'melife.demo-profile.v1';
export type AvatarAppearance = { version: 1 | 2; baseVariant: number; parts: number[]; colors: number[]; costume?: number };
export type DemoProfile = { version: 1; id: string; nickname: string; appearance: AvatarAppearance | null };
// Match the first Unity wardrobe preset; unfinished registration still keeps appearance=null.
export const DEFAULT_AVATAR: AvatarAppearance = {version:2,baseVariant:0,costume:0,parts:[0,0,2,0,0,0,2,0,0],colors:[0,6,5,7,2,6,0,0,6]};
type ProfileStorage = Pick<Storage, 'getItem' | 'setItem'>;
const legacyCounts = [4,3,3,3,5,3];
const partCounts = [4,3,4,4,5,3,3,6,3];
export function validAppearance(value: unknown): value is AvatarAppearance {
 if (!value || typeof value !== 'object') return false;
 const a = value as AvatarAppearance;
 const counts = a.version === 1 ? legacyCounts : partCounts;
 return (a.version === 1 || a.version === 2) && Number.isInteger(a.baseVariant) && a.baseVariant >= 0 && a.baseVariant < 5
  && Array.isArray(a.parts) && a.parts.length === counts.length && a.parts.every((n,i)=>Number.isInteger(n) && n >= 0 && n < counts[i])
  && Array.isArray(a.colors) && a.colors.length === counts.length && a.colors.every(n=>Number.isInteger(n) && n >= 0 && n < 8)
  && (a.version === 1 || (Number.isInteger(a.costume) && a.costume! >= 0 && a.costume! < 3));
}
export function createDemoProfile(nickname: string, id: string): DemoProfile {
 const name = nickname.replace(/[<>]/g,'').trim().slice(0,20);
 if (!name || !id) throw new Error('ニックネームを入力してください。');
 return {version:1,id,nickname:name,appearance:null};
}
export function readDemoProfile(storage: ProfileStorage): DemoProfile | null {
 const raw = storage.getItem(DEMO_PROFILE_KEY);
 try {
  const p = JSON.parse(raw || 'null');
  if (p?.version !== 1 || typeof p.id !== 'string' || !p.id || typeof p.nickname !== 'string' || !p.nickname.replace(/[<>]/g,'').trim() || p.nickname.length > 20
   || (p.appearance !== null && !validAppearance(p.appearance))) return null;
  return {version:1,id:p.id,nickname:p.nickname.replace(/[<>]/g,'').trim(),appearance:p.appearance};
 } catch { return null; }
}
export function saveDemoProfile(storage: ProfileStorage, profile: DemoProfile) {
 storage.setItem(DEMO_PROFILE_KEY, JSON.stringify({version:1,id:profile.id,nickname:profile.nickname,appearance:profile.appearance}));
}
export function updateDemoAvatar(storage: ProfileStorage, value: unknown): boolean {
 if (!value || typeof value !== 'object') return false;
 const event = value as {profileId?:unknown;nickname?:unknown;appearance?:unknown};
 const profile = readDemoProfile(storage);
 if (!profile || event.profileId !== profile.id || !validAppearance(event.appearance) || typeof event.nickname !== 'string' || !event.nickname.trim() || event.nickname.length > 20) return false;
 saveDemoProfile(storage,{...profile,nickname:event.nickname.trim(),appearance:event.appearance});
 return true;
}
