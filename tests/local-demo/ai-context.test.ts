import assert from 'node:assert/strict';
import test from 'node:test';
import {mkdtemp,cp,rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {FileLocalDemoRepository} from '../../server/local-demo/repository';
import {createDemoHandler} from '../../server/ai-navi/demo-handler';
import {LOCAL_USER_COOKIE} from '../../server/local-demo/session';
const now=Date.UTC(2026,9,24,12);
const request=(userId='',body='{}')=>new Request('http://localhost:3002/api/ai-navi/demo',{method:'POST',headers:{host:'localhost:3002',origin:'http://localhost:3002','content-type':'application/json',cookie:userId?`${LOCAL_USER_COOKIE}=${userId}`:''},body});
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
