import {readDemoProfile, type DemoProfile} from './demo-profile';

/** Resolve display data only; opening the account page never writes a wardrobe. */
export function resolveAccountAvatar(storage: Pick<Storage,'getItem'>, ownerId: string, nickname: string): DemoProfile | null {
 const saved = readDemoProfile(storage);
 if (!saved || saved.id !== ownerId || !saved.appearance) return null;
 return {...saved,nickname};
}
