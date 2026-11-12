import {BASE_AVATARS,type AvatarChoice} from './avatar-types';
export type V3Appearance={version:3;assetProfile:'v3-unified-20261104';choice:AvatarChoice};
const keys=['baseAvatarId','body','face','hair','outerwear','pants','shoes','glasses','accessory','costume','hairColor','topColor','pantsColor','skinColor'] as const;
export function isV3Choice(value:unknown):value is AvatarChoice{
 if(!value||typeof value!=='object'||Array.isArray(value))return false;
 const c=value as Record<string,unknown>;
 if(Object.keys(c).some(k=>!keys.includes(k as typeof keys[number]))||keys.some(k=>!Object.hasOwn(c,k)))return false;
 if(!BASE_AVATARS.some(m=>m.id===c.baseAvatarId)||c.body!==0)return false;
 for(const key of ['face','hair','outerwear','pants','shoes','glasses','accessory','costume'] as const){
  const n=c[key];const min=['glasses','accessory','costume'].includes(key)?-1:0;const max=key==='accessory'?4:key==='glasses'||key==='costume'?1:2;
  if(typeof n!=='number'||!Number.isInteger(n)||n<min||n>max)return false;
 }
 for(const key of ['hairColor','topColor','pantsColor','skinColor'])if(typeof c[key]!=='string'||!(key==='skinColor'&&c[key]==='')&&!/^#[\da-f]{6}$/i.test(c[key] as string))return false;
 return true;
}
export function isV3Appearance(value:unknown):value is V3Appearance{
 if(!value||typeof value!=='object')return false;
 const a=value as V3Appearance;
 return Object.keys(a).every(k=>['version','assetProfile','choice'].includes(k))&&a.version===3&&a.assetProfile==='v3-unified-20261104'&&isV3Choice(a.choice);
}
export function v3Fingerprint(choice:AvatarChoice):string{return JSON.stringify(keys.map(k=>choice[k]));}
