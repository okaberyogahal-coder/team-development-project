import {test} from 'node:test';
import assert from 'node:assert/strict';
import {InstructorStore} from '../../server/ai-instructor/store';
import {getPublicLesson} from '../../server/ai-instructor/material';
const lesson=getPublicLesson('natural-light')!;
const reply={kind:'answer' as const,text:'窓辺で試しましょう。',sectionIds:['window-light']};
test('ownership, ordering and busy requests protect the conversation',()=>{
 const s=new InstructorStore();const c=s.start('a',lesson,0);
 assert.throws(()=>s.get('b',c.conversationId,0),/NOT_FOUND/);
 assert.throws(()=>s.beginQuestion('a',c.conversationId,1,0),/STALE/);
 const lease=s.beginQuestion('a',c.conversationId,0,0);
 assert.throws(()=>s.beginQuestion('a',c.conversationId,0,0),/BUSY/);
 const next=s.finishQuestion(lease,'質問',reply,1);
 assert.equal(next.sequence,1);assert.equal(next.messages.length,2);
 assert.equal(next.remaining,19);
 assert.throws(()=>s.finishQuestion(lease,'再送',reply,1),/STALE/);
});
test('idle expiry, reset, and revoke invalidate pending answers',()=>{
 const s=new InstructorStore();let c=s.start('a',lesson,0);
 const pending=s.beginQuestion('a',c.conversationId,0,0);
 s.start('a',lesson,1);
 assert.throws(()=>s.finishQuestion(pending,'q',reply,2),/STALE/);
 c=s.start('a',lesson,0);
 assert.throws(()=>s.get('a',c.conversationId,30*60*1000),/EXPIRED/);
 c=s.start('a',lesson,0);s.revokeOwner('a');
 assert.throws(()=>s.get('a',c.conversationId,1),/NOT_FOUND/);
});
test('failed requests and restarts cannot bypass rate and total limits',()=>{
 const s=new InstructorStore();
 for(let i=0;i<20;i++){
  const c=s.start('a',lesson,i*60001);
  const lease=s.beginQuestion('a',c.conversationId,0,i*60001);s.cancelQuestion(lease);
 }
 const c=s.start('a',lesson,20*60001);
 assert.throws(()=>s.beginQuestion('a',c.conversationId,0,20*60001),/LIMIT/);
 const r=new InstructorStore();const d=r.start('b',lesson,0);
 for(let i=0;i<3;i++)r.cancelQuestion(r.beginQuestion('b',d.conversationId,0,i));
 assert.throws(()=>r.beginQuestion('b',d.conversationId,0,3),/RATE_LIMIT/);
 r.cancelQuestion(r.beginQuestion('b',d.conversationId,0,60003));
});
test('retains ten messages and limits active conversations to one hundred',()=>{
 const s=new InstructorStore();let c=s.start('a',lesson,0);
 for(let i=0;i<6;i++)c=s.finishQuestion(s.beginQuestion('a',c.conversationId,c.sequence,i*60001),'q'+i,reply,i*60001);
 assert.equal(c.messages.length,10);assert.equal(c.historyTruncated,true);assert.equal(c.messages[0].content,'q1');
 for(let i=0;i<99;i++)s.start('owner'+i,lesson,6*60001);
 assert.throws(()=>s.start('overflow',lesson,6*60001),/CAPACITY/);
 s.sweep(40*60000);assert.ok(s.start('new',lesson,40*60000));
});
test('restarting while a request is pending cannot launch a second paid request',()=>{
 const s=new InstructorStore(),c=s.start('a',lesson,0);
 const lease=s.beginQuestion('a',c.conversationId,0,0);
 const next=s.start('a',lesson,1);
 assert.throws(()=>s.beginQuestion('a',next.conversationId,0,1),/BUSY/);
 s.cancelQuestion(lease);
 assert.ok(s.beginQuestion('a',next.conversationId,0,2));
});
