import {randomUUID} from 'node:crypto';
import type {ConversationView,PublicLesson,QuizResult,TutorMessage,TutorReply} from '../../shared/ai-instructor';

const IDLE=30*60*1000;
interface Conversation {id:string;owner:string;lesson:PublicLesson;sequence:number;last:number;busy:QuestionLease|null;messages:TutorMessage[];results:Record<string,QuizResult>;truncated:boolean}
interface Budget {total:number;recent:number[];expires:number;pending:QuestionLease|null}
export interface QuestionLease {conversationId:string;owner:string;sequence:number;messages:TutorMessage[];lesson:PublicLesson;nonce:string}
export class InstructorError extends Error {constructor(public code:string){super(code);}}
export class InstructorStore {
 private conversations=new Map<string,Conversation>();
 private budgets=new Map<string,Budget>();
 start(owner:string,lesson:PublicLesson,now:number):ConversationView{
  this.sweep(now);
  const existing=[...this.conversations.values()].find(c=>c.owner===owner);
  if(!existing&&this.conversations.size>=100)throw new InstructorError('CAPACITY');
  if(!this.budgets.has(owner)){
   if(this.budgets.size>=1000)throw new InstructorError('CAPACITY');
   this.budgets.set(owner,{total:0,recent:[],expires:now+8*60*60*1000,pending:null});
  }
  if(existing)this.conversations.delete(existing.id);
  const c:Conversation={id:randomUUID(),owner,lesson:structuredClone(lesson),sequence:0,last:now,busy:null,messages:[],results:{},truncated:false};
  this.conversations.set(c.id,c);return this.view(c);
 }
 private find(owner:string,id:string,now:number):Conversation{
  const c=this.conversations.get(id);
  if(!c||c.owner!==owner)throw new InstructorError('NOT_FOUND');
  if(c.last+IDLE<=now){this.conversations.delete(id);throw new InstructorError('EXPIRED');}
  return c;
 }
 private view(c:Conversation):ConversationView{return structuredClone({conversationId:c.id,sequence:c.sequence,materialId:c.lesson.id,materialVersion:c.lesson.version,messages:c.messages,results:c.results,remaining:20-(this.budgets.get(c.owner)?.total??20),historyTruncated:c.truncated});}
 get(owner:string,id:string,now:number):ConversationView{return this.view(this.find(owner,id,now));}
 private writable(owner:string,id:string,sequence:number,now:number):Conversation{
  const c=this.find(owner,id,now);
  if(c.busy)throw new InstructorError('BUSY');
  if(c.sequence!==sequence)throw new InstructorError('STALE');
  return c;
 }
 beginQuestion(owner:string,id:string,sequence:number,now:number):QuestionLease{
  const c=this.writable(owner,id,sequence,now),budget=this.budgets.get(owner)!;
  if(budget.pending)throw new InstructorError('BUSY');
  if(budget.total>=20)throw new InstructorError('LIMIT');
  budget.recent=budget.recent.filter(at=>at>now-60000);
  if(budget.recent.length>=3)throw new InstructorError('RATE_LIMIT');
  budget.total++;budget.recent.push(now);c.last=now;
  const lease:QuestionLease={conversationId:id,owner,sequence,messages:structuredClone(c.messages),lesson:structuredClone(c.lesson),nonce:randomUUID()};
  c.busy=lease;budget.pending=lease;return lease;
 }
 finishQuestion(lease:QuestionLease,question:string,reply:TutorReply,now:number):ConversationView{
  const c=this.conversations.get(lease.conversationId);
  if(!c||c.owner!==lease.owner||c.busy!==lease||c.sequence!==lease.sequence||c.last+IDLE<=now)throw new InstructorError('STALE');
  c.messages.push({role:'user',content:question},{role:'assistant',content:reply.text,sectionIds:reply.sectionIds});
  if(c.messages.length>10){c.messages=c.messages.slice(-10);c.truncated=true;}
  this.cancelQuestion(lease);c.sequence++;c.last=now;return this.view(c);
 }
 cancelQuestion(lease:QuestionLease):void{
  const c=this.conversations.get(lease.conversationId);if(c?.busy===lease)c.busy=null;
  const budget=this.budgets.get(lease.owner);if(budget?.pending===lease)budget.pending=null;
 }
 answer(owner:string,id:string,sequence:number,questionId:string,result:QuizResult,now:number):ConversationView{
  const c=this.writable(owner,id,sequence,now);
  if(Object.hasOwn(c.results,questionId))throw new InstructorError('ALREADY_ANSWERED');
  c.results[questionId]=structuredClone(result);c.sequence++;c.last=now;return this.view(c);
 }
 reset(owner:string,id:string,now:number):void{this.find(owner,id,now);this.conversations.delete(id);}
 revokeOwner(owner:string):void{
  for(const [id,c] of this.conversations)if(c.owner===owner)this.conversations.delete(id);
  this.budgets.delete(owner);
 }
 sweep(now:number):void{
  for(const [id,c] of this.conversations)if(c.last+IDLE<=now)this.conversations.delete(id);
  for(const [owner,b] of this.budgets)if(b.expires<=now)this.revokeOwner(owner);
 }
}
