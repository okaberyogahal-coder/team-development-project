import assert from 'node:assert/strict';
import test from 'node:test';
import { createDemoContext } from '../../server/ai-navi/demo-data';
import { generateSuggestions } from '../../server/ai-navi/openai';
const context=createDemoContext('photography',Date.UTC(2026,9,23));
const config={apiKey:'test-secret',model:'test-model'};
const suggestion={theme:'季節の花を撮る',reason:'写真と花の教材の履歴をつなげた提案です。',firstStep:'身近な花を窓辺で一枚撮る。',basis:'history',evidenceIds:['complete:natural-light','view:flower-arrange'],relatedMaterialIds:[]};
const response=(payload:unknown)=>new Response(JSON.stringify(payload),{status:200});
const success=(suggestions:unknown)=>response({status:'completed',output:[{type:'message',content:[{type:'output_text',text:JSON.stringify({suggestions})}]}]});

test('builds a structured Responses request from samples without raw identities',async()=>{
 let body:any;
 const fake:typeof fetch=async(url,init)=>{
  assert.equal(url,'https://api.openai.com/v1/responses');
  assert.equal(new Headers(init?.headers).get('Authorization'),'Bearer test-secret');
  body=JSON.parse(String(init?.body));
  assert.ok(init?.signal);
  return success([suggestion]);
 };
 assert.deepEqual(await generateSuggestions(context,config,fake),[suggestion]);
 assert.equal(body.store,false);
 assert.equal(body.model,'test-model');
 assert.equal(body.text.format.type,'json_schema');
 assert.equal(body.text.format.strict,true);
 assert.ok(body.max_output_tokens>0);
 const data=JSON.parse(body.input);
 assert.ok(data.candidates.some((x:any)=>x.id==='flower-photo'));
 assert.ok(!/userId|apiKey|email/.test(body.input));
 assert.ok(data.candidates.every((x:any)=>x.id&&x.title));
});

test('missing config makes no network call',async()=>{
 let calls=0;
 const fake:typeof fetch=async()=>{calls++;return success([])};
 await assert.rejects(generateSuggestions(context,{...config,apiKey:''},fake),{code:'UNCONFIGURED'});
 await assert.rejects(generateSuggestions(context,{...config,model:''},fake),{code:'UNCONFIGURED'});
 assert.equal(calls,0);
});

test('rejects hallucinated IDs and malformed JSON',async()=>{
 await assert.rejects(generateSuggestions(context,config,async()=>success([{...suggestion,relatedMaterialIds:['missing']}])),{code:'INVALID_OUTPUT'});
 await assert.rejects(generateSuggestions(context,config,async()=>response({status:'completed',output:[{type:'message',content:[{type:'output_text',text:'broken'}]}]})),{code:'INVALID_OUTPUT'});
});

test('does not accept refusal, incomplete, or missing output as successful proposals',async()=>{
 await assert.rejects(generateSuggestions(context,config,async()=>response({status:'completed',output:[{type:'message',content:[{type:'refusal',refusal:'no'}]}]})),{code:'REFUSED'});
 await assert.rejects(generateSuggestions(context,config,async()=>response({status:'incomplete',output:[]})),{code:'INVALID_OUTPUT'});
 await assert.rejects(generateSuggestions(context,config,async()=>response({status:'completed',output:[]})),{code:'INVALID_OUTPUT'});
});

test('maps HTTP and abort failures without leaking upstream text or retrying',async()=>{
 let calls=0;
 await assert.rejects(generateSuggestions(context,config,async()=>{calls++;return new Response('test-secret',{status:500})}),{code:'UPSTREAM'});
 assert.equal(calls,1);
 await assert.rejects(generateSuggestions(context,config,async()=>{throw new DOMException('secret','AbortError')}),{code:'TIMEOUT'});
});


test('uses observed history without declared goals and sends different combinations',async()=>{
 const inputs:any[]=[];
 for(const id of ['photography','food-photo']){
  const ctx=createDemoContext(id,Date.UTC(2026,9,24));
  await generateSuggestions(ctx,config,async(_url,init)=>{inputs.push(JSON.parse(JSON.parse(String(init?.body)).input));return success([])});
 }
 assert.equal(inputs[0].interests,undefined);
 assert.equal(inputs[0].goals,undefined);
 assert.deepEqual(inputs[0].history.map((x:any)=>[x.id,x.kind]),[['complete:natural-light','complete'],['view:flower-arrange','view']]);
 assert.deepEqual(inputs[1].history.map((x:any)=>x.materialId),['natural-light','cooking']);
 assert.ok(inputs[0].history.every((x:any)=>x.title&&x.description&&x.topics.length));
});

test('rejects fabricated completion evidence and personal-history claims for newcomers',async()=>{
 await assert.rejects(generateSuggestions(context,config,async()=>success([{...suggestion,evidenceIds:['complete:flower-arrange']}])),{code:'INVALID_OUTPUT'});
 const newcomer=createDemoContext('newcomer',Date.UTC(2026,9,24));
 await assert.rejects(generateSuggestions(newcomer,config,async()=>success([suggestion])),{code:'INVALID_OUTPUT'});
 const popular={...suggestion,basis:'popularity',evidenceIds:['cooking']};
 assert.deepEqual(await generateSuggestions(newcomer,config,async()=>success([popular])),[popular]);
});

test('can suggest a theme without any related material candidates',async()=>{
 const noCatalog={...context,candidates:[]};
 assert.deepEqual(await generateSuggestions(noCatalog,config,async()=>success([suggestion])),[suggestion]);
});
