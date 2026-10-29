import assert from 'node:assert/strict';
import {randomUUID} from 'node:crypto';
import {after,test} from 'node:test';
import {createDatabase} from '../../server/db/connection';
import {readAccountProfile,saveAccountProfile} from '../../server/account/profile-store';
import {ProfileConflictError,ProfileAccessError} from '../../server/account/profile-handler';
const url=new URL(process.env.DATABASE_URL||'');
assert.ok(['localhost','127.0.0.1','[::1]'].includes(url.hostname)&&url.pathname.endsWith('_test'));
const db=createDatabase(url.href);const ids:string[]=[];
after(async()=>{try{await db.user.deleteMany({where:{id:{in:ids}}});}finally{await db.$disconnect();}});
async function user(){const result=await db.user.create({data:{email:`profile-${randomUUID()}@example.invalid`,name:'変更前',profile:{create:{nickname:'変更前',avatarId:'cat',bio:'保持する紹介',age:30}},preferences:{create:{interests:['写真'],useAgeForAi:true}}}});ids.push(result.id);return result;}
test('profile and preferences persist together, preserving uneditable fields and other accounts',async()=>{
 const first=await user(),other=await user();const before=await readAccountProfile(db,first.id);const {ownerId,...input}=before;assert.equal(ownerId,first.id);
 const saved=await saveAccountProfile(db,first.id,{...input,nickname:'新しい名前',teaches:['写真'],wantsToLearn:['花'],strengths:'聞くこと',goal:'毎日撮影'});
 assert.equal(saved.profileVersion,2);assert.equal(saved.preferencesVersion,2);assert.equal(saved.nickname,'新しい名前');
 const row=await db.user.findUniqueOrThrow({where:{id:first.id},include:{profile:true,preferences:true}});
 assert.equal(row.name,'新しい名前');assert.equal(row.profile?.avatarId,'cat');assert.equal(row.profile?.bio,'保持する紹介');assert.equal(row.profile?.age,30);assert.equal(row.preferences?.useAgeForAi,true);assert.equal(row.preferences?.goal,'毎日撮影');
 assert.equal((await readAccountProfile(db,other.id)).nickname,'変更前');
});
test('a stale preferences version rolls back the name and profile update',async()=>{
 const first=await user();const {ownerId,...input}=await readAccountProfile(db,first.id);assert.equal(ownerId,first.id);
 await db.learningPreferences.update({where:{userId:first.id},data:{version:{increment:1},goal:'別の画面の変更'}});
 await assert.rejects(()=>saveAccountProfile(db,first.id,{...input,nickname:'上書きしない'}),ProfileConflictError);
 const after=await readAccountProfile(db,first.id);assert.equal(after.nickname,'変更前');assert.equal(after.profileVersion,1);assert.equal(after.goal,'別の画面の変更');
 assert.equal((await db.user.findUniqueOrThrow({where:{id:first.id}})).name,'変更前');
});
test('stale edits and suspended accounts cannot save',async()=>{
 const first=await user();const {ownerId,...input}=await readAccountProfile(db,first.id);assert.equal(ownerId,first.id);
 await saveAccountProfile(db,first.id,{...input,nickname:'先に保存'});
 await assert.rejects(()=>saveAccountProfile(db,first.id,{...input,nickname:'古い保存'}),ProfileConflictError);
 await db.user.update({where:{id:first.id},data:{status:'SUSPENDED'}});
 const current=await readAccountProfile(db,first.id);const {ownerId:currentId,...fields}=current;assert.equal(currentId,first.id);
 await assert.rejects(()=>saveAccountProfile(db,first.id,{...fields,nickname:'停止後'}),ProfileAccessError);
 assert.equal((await readAccountProfile(db,first.id)).nickname,'先に保存');
});
test('accounts without profile records can create their initial profile atomically',async()=>{
 const first=await db.user.create({data:{name:'未設定'}});ids.push(first.id);
 const {ownerId,...input}=await readAccountProfile(db,first.id);assert.equal(ownerId,first.id);assert.equal(input.profileVersion,0);
 const result=await saveAccountProfile(db,first.id,{...input,nickname:'初回保存'});assert.equal(result.profileVersion,1);assert.equal(result.preferencesVersion,1);
});
