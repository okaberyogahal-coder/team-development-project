import {test} from 'node:test';
import assert from 'node:assert/strict';
import {createInstructorHandler} from '../../server/ai-instructor/handler';
import {InstructorStore} from '../../server/ai-instructor/store';
import {LocalSessionStore,LOCAL_SESSION_COOKIE} from '../../server/local-demo/session';
import {getPublicLesson} from '../../server/ai-instructor/material';
const lesson=getPublicLesson('natural-light')!;
function setup(){
 let now=0;let currentLesson=lesson;
 const sessions=new LocalSessionStore(),store=new InstructorStore();
 const token=sessions.issue('photography',now),other=sessions.issue('photography',now);
 let generate:NonNullable<Parameters<typeof createInstructorHandler>[0]['generate']>=async()=>({kind:'answer',text:'カーテン越しの光を試しましょう。',sectionIds:['window-light']});
 const handler=createInstructorHandler({environment:'development',enabled:true,sessions,store,config:{apiKey:'test',model:'test'},now:()=>now,generate:(...args)=>generate(...args),getLesson:()=>currentLesson});
 const request=(body?:unknown,t=token,url='http://localhost:3004/api/ai-instructor',extra:Record<string,string>={})=>handler(new Request(url,{method:body===undefined?'GET':'POST',headers:{host:new URL(url).host,origin:new URL(url).origin,'content-type':'application/json',cookie:`${LOCAL_SESSION_COOKIE}=${t}`,...extra},body:body===undefined?undefined:JSON.stringify(body)}));
 const start=async()=>{const r=await request({action:'start',materialId:'natural-light'});assert.equal(r.status,200);return r.json();};
 const question=(c:{conversationId:string;sequence:number},text='光について')=>({action:'question',conversationId:c.conversationId,sequence:c.sequence,question:text,sectionId:'window-light',mode:'question'});
 return {sessions,store,token,other,request,start,question,setNow:(n:number)=>{now=n;},setLesson:()=>{currentLesson={...lesson,version:2};},setGenerate:(g:typeof generate)=>{generate=g;},handler};
}
test('API rejects unauthorized and cross-origin access and hides answers',async()=>{
 const s=setup();assert.equal((await s.request(undefined,'')).status,401);
 assert.equal((await s.request({action:'start',materialId:'natural-light'},s.token,undefined,{origin:'http://evil.example'})).status,403);
 assert.equal((await s.request(undefined,s.token,'http://192.168.1.2:3004/api/ai-instructor')).status,403);
 const data=await(await s.request()).json();assert.equal(data.questions.length,3);
 assert.deepEqual(Object.keys(data.questions[0]).sort(),['choices','id','prompt']);
 const c=await s.start();assert.deepEqual(c.results,{});
 assert.equal((await s.request(s.question(c),s.other)).status,404);
 const answer=await(await s.request({action:'answer',conversationId:c.conversationId,sequence:0,questionId:'light',choice:0})).json();
 assert.equal(answer.results.light.correct,true);assert.deepEqual(Object.keys(answer.results),['light']);
 assert.equal((await s.request(s.question(c))).status,409);
});
test('Japanese input fits the byte budget but oversized or forged inputs are rejected',async()=>{
 const s=setup(),c=await s.start();
 assert.equal((await s.request({...s.question(c),messages:[]})).status,400);
 assert.equal((await s.request({...s.question(c),sectionId:'unknown'})).status,400);
 assert.equal((await s.request({...s.question(c),mode:{toString:'invalid'}})).status,400);
 assert.equal((await s.request(s.question(c,'あ'.repeat(1001)))).status,400);
 assert.equal((await s.request(s.question(c,'あ'.repeat(6000)))).status,413);
 assert.equal((await s.request(s.question(c,'あ'.repeat(1000)))).status,200);
 assert.equal((await s.handler(new Request('http://localhost:3004/api/ai-instructor',{method:'POST',headers:{host:'localhost:3004',origin:'http://localhost:3004','content-type':'application/json',cookie:`${LOCAL_SESSION_COOKIE}=${s.token}`},body:'{'}))).status,400);
});
test('reset and logout discard in-flight responses; revoke removes stored text',async()=>{
 for(const action of ['reset','logout','version'] as const){
  const s=setup(),c=await s.start();let release!:()=>void;
  s.setGenerate(async()=>{await new Promise<void>(resolve=>{release=resolve;});return {kind:'answer',text:'late',sectionIds:['window-light']};});
  const pending=s.request(s.question(c));
  while(!release)await new Promise(resolve=>setTimeout(resolve,0));
  assert.equal((await s.request(s.question(c))).status,429);
  if(action==='reset')assert.equal((await s.request({action:'reset',conversationId:c.conversationId})).status,200);
  if(action==='logout')s.sessions.revoke(s.token);
  if(action==='version')s.setLesson();
  release();const result=await pending;assert.equal(result.status,action==='logout'?401:409);
  if(action==='logout')assert.throws(()=>s.store.get(s.token,c.conversationId,1),/NOT_FOUND/);
 }
});
test('expiration, configuration and runtime gates are enforced',async()=>{
 const s=setup(),c=await s.start();s.setNow(30*60*1000);
 assert.equal((await s.request(s.question(c))).status,410);
 for(const config of [{environment:'production',enabled:true},{environment:'development',enabled:false}]){
  const h=createInstructorHandler({...config,config:{apiKey:'',model:''},sessions:s.sessions,store:s.store});
  assert.equal((await h(new Request('http://localhost:3004/api/ai-instructor',{headers:{host:'localhost:3004'}}))).status,404);
 }
 const h=createInstructorHandler({environment:'development',enabled:true,sessions:s.sessions,store:s.store,now:()=>0,config:{apiKey:'',model:''}});
 const d=s.store.start(s.token,lesson,0);
 const r=await h(new Request('http://localhost:3004/api/ai-instructor',{method:'POST',headers:{host:'localhost:3004',origin:'http://localhost:3004','content-type':'application/json',cookie:`${LOCAL_SESSION_COOKIE}=${s.token}`},body:JSON.stringify(s.question(d))}));
 assert.equal(r.status,503);
});
test('session expiry and revocation notify subscribers',()=>{
 const sessions=new LocalSessionStore(),seen:string[]=[];sessions.onRevoke(token=>seen.push(token));
 const a=sessions.issue('photography',0),b=sessions.issue('photography',0);
 sessions.revoke(a);assert.equal(sessions.resolve(b,8*60*60*1000),null);assert.deepEqual(seen,[a,b]);
});
