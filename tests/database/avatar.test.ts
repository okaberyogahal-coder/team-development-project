import assert from 'node:assert/strict';
import {randomUUID} from 'node:crypto';
import {after,test} from 'node:test';
import {createDatabase} from '../../server/db/connection';
import {readAccountAvatar,saveAccountAvatar} from '../../server/account/avatar-store';
import {readAccountProfile,saveAccountProfile} from '../../server/account/profile-store';
import {ProfileConflictError} from '../../server/account/profile-handler';
import {DEFAULT_AVATAR_CHOICE} from '../../shared/avatar-types';
import type {V3Appearance} from '../../shared/avatar-v3';

const url=new URL(process.env.DATABASE_URL||'');
assert.ok(['localhost','127.0.0.1','[::1]'].includes(url.hostname)&&url.pathname.endsWith('_test'));
const db=createDatabase(url.href),ids:string[]=[];
const appearance:V3Appearance={version:3,assetProfile:'v3-unified-20261104',choice:{...DEFAULT_AVATAR_CHOICE,baseAvatarId:'elderly-woman'}};
after(async()=>{try{await db.user.deleteMany({where:{id:{in:ids}}});}finally{await db.$disconnect();}});
async function user(){const row=await db.user.create({data:{email:`avatar-${randomUUID()}@example.invalid`,name:'保存確認',profile:{create:{nickname:'保存確認',gender:'female',age:60}},preferences:{create:{}}}});ids.push(row.id);return row;}

test('account appearance persists across connections and remains private to its owner',async()=>{
 const owner=await user(),other=await user();
 await saveAccountAvatar(db,owner.id,appearance,0);
 const reopened=createDatabase(url.href);
 try{assert.deepEqual(await readAccountAvatar(reopened,owner.id),{ownerId:owner.id,appearance,version:1});}
 finally{await reopened.$disconnect();}
 assert.equal((await readAccountAvatar(db,other.id)).appearance,null);
});

test('simultaneous saves accept one version and preserve the winning outfit',async()=>{
 const owner=await user();
 const second:V3Appearance={...appearance,choice:{...appearance.choice,topColor:'#FFFFFF'}};
 const results=await Promise.allSettled([saveAccountAvatar(db,owner.id,appearance,0),saveAccountAvatar(db,owner.id,second,0)]);
 const winner=results.find(result=>result.status==='fulfilled');
 assert.equal(results.filter(result=>result.status==='fulfilled').length,1);
 const loser=results.find(result=>result.status==='rejected');
 assert.ok(loser?.status==='rejected'&&loser.reason instanceof ProfileConflictError);
 assert.ok(winner?.status==='fulfilled');
 assert.deepEqual(await readAccountAvatar(db,owner.id),winner.value);
});

test('profile edits and avatar saves preserve each other without sharing a version counter',async()=>{
 const owner=await user();const {ownerId,...fields}=await readAccountProfile(db,owner.id);assert.equal(ownerId,owner.id);
 await Promise.all([saveAccountAvatar(db,owner.id,appearance,0),saveAccountProfile(db,owner.id,{...fields,nickname:'プロフィール変更'})]);
 assert.deepEqual((await readAccountAvatar(db,owner.id)).appearance,appearance);
 assert.equal((await readAccountProfile(db,owner.id)).nickname,'プロフィール変更');
 assert.equal((await readAccountAvatar(db,owner.id)).version,1);
});
