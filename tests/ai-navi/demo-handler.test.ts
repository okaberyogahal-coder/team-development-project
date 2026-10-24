import assert from 'node:assert/strict';
import test from 'node:test';
import { createDemoHandler } from '../../server/ai-navi/demo-handler';
import { AiNaviError } from '../../server/ai-navi/openai';
const config={apiKey:'secret',model:'test-model'};
const request=(body='{"profileId":"photography"}',headers:Record<string,string>={})=>new Request('http://localhost:3002/api/ai-navi/demo',{method:'POST',headers:{host:'localhost:3002',origin:'http://localhost:3002','content-type':'application/json',...headers},body});
const generate=async()=>[{theme:'季節の花を撮る',reason:'写真と花の履歴から',firstStep:'身近な花を撮る',basis:'history' as const,evidenceIds:['complete:natural-light','view:flower-arrange'],relatedMaterialIds:[]}];

test('non-development, public hosts and mismatched origins never call OpenAI',async()=>{
 let calls=0;
 const deps={config,generate:async()=>{calls++;return generate()}};
 assert.equal((await createDemoHandler({...deps,environment:'production'})(request())).status,404);
 const handler=createDemoHandler({...deps,environment:'development',enabled:true});
 for(const r of [request(undefined,{origin:'https://evil.example'}),request(undefined,{origin:''}),request(undefined,{host:'evil.example'}),new Request('http://evil.example/api/ai-navi/demo',{method:'POST',headers:{origin:'http://evil.example',host:'evil.example'},body:'{}'})]) assert.equal((await handler(r)).status,403);
 assert.equal(calls,0);
});

test('rejects wrong content type, oversized body and malformed profile before generating',async()=>{
 let calls=0;
 const handler=createDemoHandler({environment:'development',enabled:true,config,generate:async()=>{calls++;return generate()}});
 assert.equal((await handler(request('{}',{'content-type':'text/plain'}))).status,415);
 assert.equal((await handler(request(' '.repeat(1025)))).status,413);
 assert.equal((await handler(request('broken'))).status,400);
 assert.equal((await handler(request('{"profileId":"unknown"}'))).status,400);
 assert.equal((await handler(request('{"profileId":"photography","privateData":"x"}'))).status,400);
 assert.equal(calls,0);
 assert.equal((await handler(request())).status,200);
});

test('missing config produces a helpful safe error and does not consume request quota',async()=>{
 const handler=createDemoHandler({environment:'development',enabled:true,config:{...config,apiKey:''},generate});
 const result=await handler(request());
 assert.equal(result.status,503);
 assert.equal((await result.json()).error.code,'UNCONFIGURED');
});

test('allows three requests per 60 seconds then recovers, returning aggregate sample data only',async()=>{
 let now=Date.UTC(2026,9,23);
 const handler=createDemoHandler({environment:'development',enabled:true,config,generate,now:()=>now});
 for(let i=0;i<3;i++){
  const result=await handler(request());assert.equal(result.status,200);
  assert.equal(result.headers.get('cache-control'),'no-store');
  const json=await result.json();assert.equal(json.popularity.activeUsers,5);
  assert.ok(!JSON.stringify(json).includes('sample-0'));
 }
 assert.equal((await handler(request())).status,429);
 now+=60000;
 assert.equal((await handler(request())).status,200);
});

test('rejects a concurrent request and releases the lock after completion',async()=>{
 let done!:()=>void;
 const pending=new Promise<void>(resolve=>{done=resolve});
 const handler=createDemoHandler({environment:'development',enabled:true,config,generate:async()=>{await pending;return generate()}});
 const first=handler(request());
 await new Promise(resolve=>setTimeout(resolve,10));
 assert.equal((await handler(request())).status,429);
 done();assert.equal((await first).status,200);
 assert.equal((await handler(request())).status,200);
});

test('sanitizes errors and distinguishes timeout, refusal and invalid output',async()=>{
 for(const code of ['TIMEOUT','REFUSED','INVALID_OUTPUT'] as const){
  const handler=createDemoHandler({environment:'development',enabled:true,config,generate:async()=>{throw new AiNaviError(code)}});
  assert.equal((await (await handler(request())).json()).error.code,code);
 }
 const handler=createDemoHandler({environment:'development',enabled:true,config,generate:async()=>{throw new Error('secret')}});
 const result=await handler(request());assert.equal(result.status,502);
 assert.ok(!(await result.text()).includes('secret'));
});

test('ordinary development stays disabled even with matching spoofable headers and valid API credentials',async()=>{
 let calls=0;
 const handler=createDemoHandler({environment:'development',config,generate:async()=>{calls++;return generate()}});
 assert.equal((await handler(request())).status,404);
 assert.equal(calls,0);
});
