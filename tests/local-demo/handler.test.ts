import assert from 'node:assert/strict';
import test from 'node:test';
import {mkdtemp,cp,rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {FileLocalDemoRepository} from '../../server/local-demo/repository';
import {createLocalDemoHandler} from '../../server/local-demo/handler';
import {LOCAL_USER_COOKIE} from '../../server/local-demo/session';

async function setup(t:{after(fn:()=>Promise<void>):void}){
 const root=await mkdtemp(join(tmpdir(),'melife-local-api-'));await cp('data/local-demo',root,{recursive:true});
 t.after(()=>rm(root,{recursive:true,force:true}));
 const repo=new FileLocalDemoRepository(root);
 const handler=createLocalDemoHandler({repo,environment:'development',enabled:true,now:()=>Date.UTC(2026,9,24,12)});
 return {repo,handler};
}
function request(body?:unknown,cookie='',headers:Record<string,string>={}){
 return new Request('http://localhost:3002/api/local-demo',{method:body===undefined?'GET':'POST',headers:{host:'localhost:3002',origin:'http://localhost:3002','content-type':'application/json',cookie,...headers},...(body===undefined?{}:{body:JSON.stringify(body)})});
}

test('lists fictional users, selects one with a private local cookie, and restores selected state',async t=>{
 const {handler}=await setup(t);
 const initial=await (await handler(request())).json();assert.equal(initial.currentUser,null);assert.equal(initial.users.length,3);
 const login=await handler(request({action:'login',userId:'photography'}));assert.equal(login.status,200);
 const cookie=login.headers.get('set-cookie')!;
 assert.match(cookie,/HttpOnly/i);assert.match(cookie,/SameSite=Lax/i);assert.match(cookie,/Path=\//i);assert.match(cookie,/Max-Age=604800/i);
 const state=await (await handler(request(undefined,cookie.split(';')[0]))).json();assert.equal(state.currentUser.id,'photography');
 assert.equal(login.headers.get('cache-control'),'no-store');
});

test('records only for the selected user, distinguishing view and completion; logout preserves files',async t=>{
 const {handler,repo}=await setup(t);const cookie=`${LOCAL_USER_COOKIE}=newcomer`;
 assert.equal((await handler(request({action:'record',materialId:'digital',kind:'view'},cookie))).status,200);
 assert.deepEqual((await repo.getUser('newcomer')).events.map(x=>x.kind),['view']);
 await handler(request({action:'record',materialId:'digital',kind:'complete'},cookie));
 assert.equal((await repo.getUser('photography')).events.length,2);
 const before=await repo.getUser('newcomer');const logout=await handler(request({action:'logout'},cookie));
 assert.match(logout.headers.get('set-cookie')!,/Max-Age=0/);
 assert.deepEqual(await repo.getUser('newcomer'),before);
 assert.equal((await handler(request({action:'record',materialId:'digital',kind:'view'}))).status,401);
});

test('rejects unknown selection, user spoofing, timestamps, malformed actions and oversized input',async t=>{
 const {handler}=await setup(t);const cookie=`${LOCAL_USER_COOKIE}=newcomer`;
 for(const body of [{action:'login',userId:'../x'},{action:'record',materialId:'digital',kind:'view',userId:'photography'},{action:'record',materialId:'digital',kind:'view',at:'2026-01-01'},{action:'record',materialId:'missing',kind:'view'},{action:'record',materialId:'digital',kind:'other'},{action:'x'}]) assert.equal((await handler(request(body,cookie))).status,400);
 assert.equal((await handler(request({action:'record',materialId:'digital',kind:'view'},`${LOCAL_USER_COOKIE}=unknown`))).status,401);
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
