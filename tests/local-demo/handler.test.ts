import assert from 'node:assert/strict';
import test from 'node:test';
import {mkdtemp,cp,rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {FileLocalDemoRepository} from '../../server/local-demo/repository';
import {createLocalDemoHandler} from '../../server/local-demo/handler';
import {testCookie} from './test-auth';
import {LocalSessionStore,getLocalSessionStore} from '../../server/local-demo/session';
import {createCredentialVerifier,LoginAttemptLimiter} from '../../server/local-demo/credentials';

async function setup(t:{after(fn:()=>Promise<void>):void}){
 const root=await mkdtemp(join(tmpdir(),'melife-local-api-'));await cp('tests/fixtures/local-demo',root,{recursive:true});
 t.after(()=>rm(root,{recursive:true,force:true}));
 const repo=new FileLocalDemoRepository(root);
 const handler=createLocalDemoHandler({repo,environment:'development',enabled:true,now:()=>Date.UTC(2026,9,24,12),verify:createCredentialVerifier(root),attempts:new LoginAttemptLimiter()});
 return {repo,handler};
}
function request(body?:unknown,cookie='',headers:Record<string,string>={}){
 return new Request('http://localhost:3002/api/local-demo',{method:body===undefined?'GET':'POST',headers:{host:'localhost:3002',origin:'http://localhost:3002','content-type':'application/json',cookie,...headers},...(body===undefined?{}:{body:JSON.stringify(body)})});
}

test('lists fictional users, selects one with a private local cookie, and restores selected state',async t=>{
 const {handler}=await setup(t);
 const initial=await (await handler(request())).json();assert.equal(initial.currentUser,null);assert.equal(initial.users.length,3);
 const login=await handler(request({action:'login',email:'sora@example.test',password:'MilifeDemo2026!'}));assert.equal(login.status,200);
 const cookie=login.headers.get('set-cookie')!;
 assert.match(cookie,/HttpOnly/i);assert.match(cookie,/SameSite=Lax/i);assert.match(cookie,/Path=\//i);assert.match(cookie,/Max-Age=28800/i);
 const state=await (await handler(request(undefined,cookie.split(';')[0]))).json();assert.equal(state.currentUser.id,'photography');
 assert.equal(login.headers.get('cache-control'),'no-store');
});

test('records only for the selected user, distinguishing view and completion; logout preserves files',async t=>{
 const {handler,repo}=await setup(t);const cookie=testCookie('newcomer');
 const revision=async()=> (await (await handler(request(undefined,cookie))).json()).revision;
 assert.equal((await handler(request({action:'record',materialId:'digital',kind:'view'},cookie,{'if-match':await revision()}))).status,200);
 assert.deepEqual((await repo.getUser('newcomer')).events.map(x=>x.kind),['view']);
 await handler(request({action:'record',materialId:'digital',kind:'complete'},cookie,{'if-match':await revision()}));
 assert.equal((await repo.getUser('photography')).events.length,2);
 const before=await repo.getUser('newcomer');const logout=await handler(request({action:'logout'},cookie));
 assert.match(logout.headers.get('set-cookie')!,/Max-Age=0/);
 assert.deepEqual(await repo.getUser('newcomer'),before);
 assert.equal((await handler(request({action:'record',materialId:'digital',kind:'view'}))).status,401);
});

test('rejects unknown selection, user spoofing, timestamps, malformed actions and oversized input',async t=>{
 const {handler}=await setup(t);const cookie=testCookie('newcomer');
 for(const body of [{action:'login',userId:'../x'},{action:'record',materialId:'digital',kind:'view',userId:'photography'},{action:'record',materialId:'digital',kind:'view',at:'2026-01-01'},{action:'record',materialId:'missing',kind:'view'},{action:'record',materialId:'digital',kind:'other'},{action:'x'}]) assert.equal((await handler(request(body,cookie))).status,400);
 assert.equal((await handler(request({action:'record',materialId:'digital',kind:'view'},'melife.local-demo.user.v1=newcomer'))).status,401);
 assert.equal((await handler(request('x'.repeat(1025)))).status,413);
 assert.equal((await handler(request({action:'logout'},'',{'content-type':'text/plain'}))).status,415);
 assert.equal((await handler(request({action:'logout'},'',{origin:'https://evil.example'}))).status,403);
 assert.equal((await handler(request(undefined,'',{host:'evil.example'}))).status,403);
});

test('ordinary dev and production stay disabled, while invalid stored data is reported safely',async t=>{
 const {repo}=await setup(t);
 for(const options of [{environment:'production',enabled:true},{environment:'development',enabled:false}]){
  const disabled=createLocalDemoHandler({repo,...options});assert.equal((await disabled(request())).status,404);
 }
 const broken={listUsers:async()=>{throw new Error('/private/secret/path')},listMaterials:()=>repo.listMaterials(),getUser:repo.getUser.bind(repo),recordEvent:repo.recordEvent.bind(repo)};
 const handler=createLocalDemoHandler({repo:broken,environment:'development',enabled:true});
 const response=await handler(request());assert.equal(response.status,500);assert.ok(!(await response.text()).includes('/private/secret'));
});

test('stale displayed user cannot record into another cookie-selected user',async t=>{
 const {handler,repo}=await setup(t);
 const state=await (await handler(request(undefined,testCookie('photography')))).json();
 const before=await repo.getUser('newcomer');
 const response=await handler(request({action:'record',materialId:'digital',kind:'view'},testCookie('newcomer'),{'if-match':state.revision}));
 assert.equal(response.status,409);assert.deepEqual(await repo.getUser('newcomer'),before);
});


test('credentials reject ID bypass, rotate and revoke sessions, sharing across handlers',async t=>{
 const {repo}=await setup(t);const root=await mkdtemp(join(tmpdir(),'melife-login-'));await cp('tests/fixtures/local-demo',root,{recursive:true});t.after(()=>rm(root,{recursive:true,force:true}));
 const options={repo,environment:'development',enabled:true,verify:createCredentialVerifier(root),attempts:new LoginAttemptLimiter()};
 const handler=createLocalDemoHandler(options);const other=createLocalDemoHandler(options);
 assert.equal((await handler(request({action:'login',userId:'photography'}))).status,400);
 const bad=await handler(request({action:'login',email:'sora@example.test',password:'wrong'}));
 const unknown=await handler(request({action:'login',email:'nobody@example.test',password:'wrong'}));
 assert.equal(bad.status,401);assert.deepEqual(await bad.json(),await unknown.json());
 const login=async(cookie='')=>handler(request({action:'login',email:'sora@example.test',password:'MilifeDemo2026!'},cookie));
 const cookie=(await login()).headers.get('set-cookie')!.split(';')[0];
 assert.equal((await (await other(request(undefined,cookie))).json()).currentUser.id,'photography');
 const rotated=(await login(cookie)).headers.get('set-cookie')!.split(';')[0];assert.notEqual(rotated,cookie);
 assert.equal((await (await other(request(undefined,cookie))).json()).currentUser,null);
 await handler(request({action:'logout'},rotated));assert.equal((await (await other(request(undefined,rotated))).json()).currentUser,null);
});

test('login failure quota is shared across handlers, does not reset on success, and recovers',async t=>{
 const {repo}=await setup(t);let now=1000;const attempts=new LoginAttemptLimiter();const sessions=new LocalSessionStore();
 const options={repo,environment:'development',enabled:true,now:()=>now,attempts,sessions,verify:async(_email:string,password:string)=>password==='correct'?'photography' as const:null};
 const handler=createLocalDemoHandler(options),other=createLocalDemoHandler(options);
 const bad={action:'login',email:'sora@example.test',password:'wrong'};
 for(let i=0;i<19;i++)assert.equal((await handler(request(bad))).status,401);
 assert.equal((await handler(request({...bad,password:'correct'}))).status,200);
 assert.equal((await other(request(bad))).status,401);
 assert.equal((await handler(request(bad))).status,429);
 now+=600000;assert.equal((await handler(request({...bad,password:'correct'}))).status,200);
});

test('broken credential data yields a safe error without leaking paths or hashes',async t=>{
 const {repo}=await setup(t);const handler=createLocalDemoHandler({repo,environment:'development',enabled:true,attempts:new LoginAttemptLimiter(),verify:async()=>{throw new Error('/private/sensitive/hash')}});
 const result=await handler(request({action:'login',email:'sora@example.test',password:'x'}));assert.equal(result.status,500);assert.ok(!(await result.text()).includes('/private/sensitive'));
});
