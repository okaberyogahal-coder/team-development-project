import {test} from 'node:test';
import assert from 'node:assert/strict';
import {generateTutorReply} from '../../server/ai-instructor/openai';
import {validateTutorReply} from '../../server/ai-instructor/output';
import {getPublicLesson} from '../../server/ai-instructor/material';
const lesson=getPublicLesson('natural-light')!;
const reply={kind:'answer',text:'カーテン越しの光を試しましょう。',sectionIds:['window-light']};
const envelope=(text:unknown)=>({status:'completed',output:[{type:'message',role:'assistant',content:[{type:'output_text',text:JSON.stringify(text)}]}]});
test('answers must cite existing sections and stay within the output contract',()=>{
 assert.deepEqual(validateTutorReply(reply,lesson),reply);
 for(const value of [{...reply,sectionIds:['missing']},{...reply,sectionIds:[]},{...reply,text:'あ'.repeat(2001)},{...reply,kind:'other'},{...reply,kind:['out_of_scope'],sectionIds:[]},{...reply,extra:1}])assert.throws(()=>validateTutorReply(value,lesson),/INVALID_OUTPUT/);
 assert.deepEqual(validateTutorReply({kind:'out_of_scope',text:'この教材では確認できません。',sectionIds:[]},lesson).sectionIds,[]);
});
test('Responses request sends only lesson and bounded conversation with no server storage',async()=>{
 const fetcher:typeof fetch=async(_url,init)=>{
  const body=JSON.parse(init!.body as string);
  assert.equal(body.store,false);assert.equal(body.max_output_tokens,1200);
  assert.equal(body.text.format.type,'json_schema');
  assert.deepEqual(JSON.parse(body.input).lesson,lesson);
  assert.equal(body.model,'configured-model');
  return Response.json(envelope(reply));
 };
 assert.deepEqual(await generateTutorReply({lesson,messages:[],question:'光は？'},{apiKey:'test',model:'configured-model'},fetcher),reply);
});
test('unconfigured, failed, refused and malformed responses are distinct',async()=>{
 const input={lesson,messages:[],question:'q'},config={apiKey:'test',model:'test'};
 await assert.rejects(generateTutorReply(input,{apiKey:'',model:''}),/UNCONFIGURED/);
 for(const [response,code] of [
  [new Response('',{status:500}),'UPSTREAM'],
  [Response.json({status:'incomplete',output:[]}), 'INVALID_OUTPUT'],
  [Response.json({status:'completed',output:[{type:'message',content:[{type:'refusal'}]}]}),'REFUSED'],
  [Response.json(envelope({...reply,sectionIds:['not-real']})),'INVALID_OUTPUT'],
 ] as const)await assert.rejects(generateTutorReply(input,config,async()=>response),new RegExp(code));
});
test('abort is reported as timeout',async()=>{
 await assert.rejects(generateTutorReply({lesson,messages:[],question:'q'},{apiKey:'test',model:'test'},async()=>{throw new DOMException('aborted','AbortError');}),/TIMEOUT/);
});
