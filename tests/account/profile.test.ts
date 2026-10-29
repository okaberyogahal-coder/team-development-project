import assert from 'node:assert/strict';
import {test} from 'node:test';
import {profileEditInput} from '../../shared/account-profile';
import {createProfileHandlers,ProfileConflictError} from '../../server/account/profile-handler';
const input={nickname:'  みらい  ',interests:['写真','写真'],wantsToLearn:['園芸'],teaches:[],strengths:'聞くこと',goal:'花を撮る',profileVersion:1,preferencesVersion:2};
const request=(body:unknown=input,origin='http://localhost:3002')=>new Request('http://localhost:3002/api/account/profile',{method:'PATCH',headers:{origin,'Content-Type':'application/json','X-Profile-Owner':'session-user'},body:JSON.stringify(body)});
test('profile validation trims and deduplicates without accepting identity or credentials',()=>{
 const parsed=profileEditInput(input);assert.equal(parsed.nickname,'みらい');assert.deepEqual(parsed.interests,['写真']);
 for(const change of [{userId:'someone-else'},{email:'other@example.test'},{password:'secret'},{avatar:'cat'},{nickname:' '},{nickname:'a'.repeat(21)},{interests:['a'.repeat(31)]},{teaches:Array(11).fill('a')},{goal:'a'.repeat(501)},{profileVersion:-1},{preferencesVersion:1.5},{strengths:'bad\0text'}])assert.throws(()=>profileEditInput({...input,...change}));
 assert.deepEqual(profileEditInput({...input,interests:[],wantsToLearn:[],teaches:[],strengths:'',goal:''}).interests,[]);
});
test('only authenticated identity is passed to storage and responses are private',async()=>{
 let saved:unknown;
 const handlers=createProfileHandlers({getUser:async()=>({id:'session-user'}),read:async id=>({id}),save:async(id,data)=>{saved={id,data};return {saved:true};},allowSave:async()=>true});
 const response=await handlers.PATCH(request());assert.equal(response.status,200);assert.deepEqual(saved,{id:'session-user',data:profileEditInput(input)});assert.equal(response.headers.get('cache-control'),'no-store');
 assert.deepEqual(await (await handlers.GET()).json(),{id:'session-user'});
});
test('unsigned, cross-origin, malformed and rate-limited saves never write',async()=>{
 let writes=0;const deps={getUser:async()=>({id:'session-user'}),read:async()=>({}),save:async()=>{writes++;return {};},allowSave:async()=>true};
 assert.equal((await createProfileHandlers({...deps,getUser:async()=>null}).PATCH(request())).status,401);
 assert.equal((await createProfileHandlers({...deps,getUser:async()=>null}).GET()).status,401);
 assert.equal((await createProfileHandlers(deps).PATCH(request(input,'https://attacker.invalid'))).status,403);
 assert.equal((await createProfileHandlers(deps).PATCH(request({...input,userId:'other'}))).status,400);
 assert.equal((await createProfileHandlers({...deps,allowSave:async()=>false}).PATCH(request())).status,429);
 assert.equal((await createProfileHandlers({...deps,getUser:async()=>({id:'different-user'})}).PATCH(request())).status,409);
 assert.equal(writes,0);
});
test('conflicts and storage failures are distinguished without leaking details',async()=>{
 const deps={getUser:async()=>({id:'session-user'}),read:async()=>({}),save:async()=>{throw new ProfileConflictError();},allowSave:async()=>true};
 assert.equal((await createProfileHandlers(deps).PATCH(request())).status,409);
 const response=await createProfileHandlers({...deps,save:async()=>{throw new Error('database-password-secret');}}).PATCH(request());
 assert.equal(response.status,503);assert.ok(!(await response.text()).includes('database-password-secret'));
});
