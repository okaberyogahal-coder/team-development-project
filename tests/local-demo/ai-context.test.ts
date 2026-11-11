import assert from 'node:assert/strict';
import test from 'node:test';
import {mkdtemp,cp,rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {FileLocalDemoRepository} from '../../server/local-demo/repository';
import {createDemoHandler} from '../../server/ai-navi/demo-handler';
import {testCookie} from './test-auth';
import {isLocalUserId} from '../../shared/local-demo';
import {getLocalSessionStore,getSessionToken} from '../../server/local-demo/session';
const now=Date.UTC(2026,9,24,12);
const request=(userId='',body='{}')=>new Request('http://localhost:3002/api/ai-navi/demo',{method:'POST',headers:{host:'localhost:3002',origin:'http://localhost:3002','content-type':'application/json',cookie:isLocalUserId(userId)?testCookie(userId):''},body});
async function setup(t:{after(fn:()=>Promise<void>):void}){
 const root=await mkdtemp(join(tmpdir(),'melife-ai-context-'));await cp('tests/fixtures/local-demo',root,{recursive:true});t.after(()=>rm(root,{recursive:true,force:true}));
 return new FileLocalDemoRepository(root);
}

test('AI requires selected fictional user and accepts no client-supplied case or history',async t=>{
 const repo=await setup(t);let calls=0;
 const handler=createDemoHandler({repo,environment:'development',enabled:true,config:{apiKey:'test',model:'test'},now:()=>now,generate:async context=>{calls++;assert.deepEqual(context.history.map(x=>x.materialId),['natural-light','cooking']);return []}});
 assert.equal((await handler(request())).status,401);
 assert.equal((await handler(request('unknown'))).status,401);
 assert.equal((await handler(request('food-photo','{"profileId":"photography"}'))).status,400);
 const result=await handler(request('food-photo'));assert.equal(result.status,200);assert.equal(calls,1);
 assert.match((await result.json()).revision,/^[a-f0-9]{64}$/);
});

test('a history change while AI works discards the old response',async t=>{
 const repo=await setup(t);let release!:()=>void;
 const pending=new Promise<void>(resolve=>{release=resolve});
 const handler=createDemoHandler({repo,environment:'development',enabled:true,config:{apiKey:'test',model:'test'},now:()=>now,generate:async()=>{await pending;return []}});
 const result=handler(request('newcomer'));
 await new Promise(resolve=>setTimeout(resolve,30));
 await repo.recordEvent('newcomer','digital','view',new Date(now));release();
 const response=await result;assert.equal(response.status,409);assert.equal((await response.json()).error.code,'STALE');
});


test('AI rejects forged sessions and discards results when session is revoked during generation',async t=>{
 const repo=await setup(t);let release!:()=>void;let started!:()=>void;
 const pending=new Promise<void>(resolve=>{release=resolve});const entered=new Promise<void>(resolve=>{started=resolve});
 const handler=createDemoHandler({repo,environment:'development',enabled:true,config:{apiKey:'test',model:'test'},generate:async()=>{started();await pending;return []}});
 const forged=new Request('http://localhost:3002/api/ai-navi/demo',{method:'POST',headers:{host:'localhost:3002',origin:'http://localhost:3002','content-type':'application/json',cookie:'melife.local-demo.user.v1=photography'},body:'{}'});
 assert.equal((await handler(forged)).status,401);
 const req=request('photography');const result=handler(req);await entered;
 getLocalSessionStore().revoke(getSessionToken(req));release();assert.equal((await result).status,401);
});

test('AI discards results when the session expires during generation',async t=>{
 const repo=await setup(t);const {LocalSessionStore,LOCAL_SESSION_COOKIE}=await import('../../server/local-demo/session');
 const sessions=new LocalSessionStore();let clock=1000;const token=sessions.issue('photography',clock);
 const handler=createDemoHandler({repo,sessions,environment:'development',enabled:true,config:{apiKey:'test',model:'test'},now:()=>clock,generate:async()=>{clock+=8*60*60*1000;return []}});
 const req=request();req.headers.set('cookie',`${LOCAL_SESSION_COOKIE}=${token}`);
 assert.equal((await handler(req)).status,401);
});

test('logout during state loading prevents any AI generation call',async t=>{
 const repo=await setup(t);const {LocalSessionStore,LOCAL_SESSION_COOKIE}=await import('../../server/local-demo/session');
 const sessions=new LocalSessionStore();const token=sessions.issue('photography',now);
 let release!:()=>void;let started!:()=>void;let calls=0;
 const pending=new Promise<void>(resolve=>{release=resolve});const entered=new Promise<void>(resolve=>{started=resolve});
 const delayed={listUsers:async()=>{started();await pending;return repo.listUsers();},listMaterials:repo.listMaterials.bind(repo),getUser:repo.getUser.bind(repo),recordEvent:repo.recordEvent.bind(repo)};
 const handler=createDemoHandler({repo:delayed,sessions,environment:'development',enabled:true,now:()=>now,config:{apiKey:'test',model:'test'},generate:async()=>{calls++;return []}});
 const req=request();req.headers.set('cookie',`${LOCAL_SESSION_COOKIE}=${token}`);
 const result=handler(req);await entered;sessions.revoke(token);release();
 assert.equal((await result).status,401);assert.equal(calls,0);
});
