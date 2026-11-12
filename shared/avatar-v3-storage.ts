import {createDemoProfile,readDemoProfile,saveDemoProfile} from './demo-profile';
import {isV3Appearance,isV3Choice,type V3Appearance} from './avatar-v3';
import type {AvatarChoice} from './avatar-types';
type Store=Pick<Storage,'getItem'|'setItem'>;
function ownerKey(ownerId:string){if(!ownerId||ownerId.length>100)throw new Error('ログイン状態を確認してください。');return encodeURIComponent(ownerId);}
export function readV3Draft(storage:Pick<Storage,'getItem'>,ownerId:string):AvatarChoice|null{
 const raw=storage.getItem('melife.avatar.v3.draft:'+ownerKey(ownerId));
 try{const data=JSON.parse(raw||'null');return data?.version===1&&data.ownerId===ownerId&&isV3Choice(data.choice)?data.choice:null;}catch{return null;}
}
export function writeV3Draft(storage:Store,ownerId:string,choice:AvatarChoice){
 if(!isV3Choice(choice))throw new Error('利用できないアバター設定です。');
 storage.setItem('melife.avatar.v3.draft:'+ownerKey(ownerId),JSON.stringify({version:1,ownerId,choice}));
}
export function completeV3Avatar(storage:Store,ownerId:string,nickname:string,appearance:V3Appearance){
 if(!isV3Appearance(appearance))throw new Error('利用できないアバター設定です。');
 const key=ownerKey(ownerId);const previous=readDemoProfile(storage);
 if(previous?.id===ownerId&&previous.appearance&&!isV3Appearance(previous.appearance))
  storage.setItem('melife.avatar.previous:'+key,JSON.stringify(previous));
 // One final canonical write enables the new outfit. Drafts and backups never do.
 saveDemoProfile(storage,{...createDemoProfile(nickname,ownerId),appearance});
}
