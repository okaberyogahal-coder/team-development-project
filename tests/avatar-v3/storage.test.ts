import {test} from 'node:test';
import assert from 'node:assert/strict';
import {DEFAULT_AVATAR_CHOICE,BASE_AVATARS} from '../../shared/avatar-types';
import {isV3Appearance,type V3Appearance} from '../../shared/avatar-v3';
import {readV3Draft,writeV3Draft,completeV3Avatar} from '../../shared/avatar-v3-storage';
import {DEFAULT_AVATAR,DEMO_PROFILE_KEY,readDemoProfile} from '../../shared/demo-profile';
import {resolveAccountAvatar} from '../../shared/account-avatar';
const appearance:V3Appearance={version:3,assetProfile:'v3-unified-20261104',choice:{...DEFAULT_AVATAR_CHOICE,baseAvatarId:'young-man'}};
function storage(){const data=new Map<string,string>();return {getItem:(k:string)=>data.get(k)||null,setItem:(k:string,v:string)=>{data.set(k,v);}};}
test('all six models validate and invalid V3 values are not silently repaired',()=>{
 for(const model of BASE_AVATARS)assert.equal(isV3Appearance({...appearance,choice:{...appearance.choice,baseAvatarId:model.id}}),true);
 for(const choice of [{...appearance.choice,baseAvatarId:'unknown'},{...appearance.choice,face:3},{...appearance.choice,hairColor:'red'},{...appearance.choice,teaches:['secret']}])assert.equal(isV3Appearance({...appearance,choice}),false);
});
test('drafts are owner scoped and never replace completed legacy appearances',()=>{
 const s=storage();s.setItem(DEMO_PROFILE_KEY,JSON.stringify({version:1,id:'a',nickname:'以前',appearance:DEFAULT_AVATAR}));
 const before=s.getItem(DEMO_PROFILE_KEY);writeV3Draft(s,'a',appearance.choice);assert.equal(readV3Draft(s,'b'),null);assert.deepEqual(readV3Draft(s,'a'),appearance.choice);assert.equal(s.getItem(DEMO_PROFILE_KEY),before);
 completeV3Avatar(s,'a','現在',appearance);assert.deepEqual(readDemoProfile(s)?.appearance,appearance);assert.equal(resolveAccountAvatar(s,'b','別人'),null);assert.equal(resolveAccountAvatar(s,'a','現在')?.nickname,'現在');
});
test('a failed final write preserves the completed outfit and reports failure',()=>{
 const s=storage();const old=JSON.stringify({version:1,id:'a',nickname:'名前',appearance:DEFAULT_AVATAR});s.setItem(DEMO_PROFILE_KEY,old);
 const blocked={getItem:s.getItem,setItem:(key:string,value:string)=>{if(key===DEMO_PROFILE_KEY)throw new Error('full');s.setItem(key,value);}};
 assert.throws(()=>completeV3Avatar(blocked,'a','名前',appearance),/full/);assert.equal(s.getItem(DEMO_PROFILE_KEY),old);
});
