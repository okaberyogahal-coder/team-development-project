import type {PublicLesson} from '../../shared/ai-instructor';
import {getLocalSessionStore,getSessionToken,type LocalSessionStore} from '../local-demo/session';
import {guardLocalRequest,exactKeys,localJSON,LocalHttpError} from '../local-demo/http';
import {getPublicLesson,getPublicQuestions,gradeQuestion} from './material';
import {generateTutorReply} from './openai';
import {readInstructorJSON} from './http';
import {InstructorError,InstructorStore} from './store';

const statuses:Record<string,number>={NOT_FOUND:404,EXPIRED:410,STALE:409,BUSY:429,LIMIT:429,RATE_LIMIT:429,CAPACITY:429,ALREADY_ANSWERED:409,UNCONFIGURED:503,TIMEOUT:504,UPSTREAM:502,INVALID_OUTPUT:502,REFUSED:502};
const messages:Record<string,string>={
 UNSELECTED:'ローカル検証用のログインが必要です。',NOT_FOUND:'この学習は利用できません。もう一度開始してください。',EXPIRED:'30分間操作がなかったため、学習を終了しました。',STALE:'学習内容が更新されました。もう一度開始してください。',BUSY:'前の質問を処理しています。完了までお待ちください。',LIMIT:'このログインでの質問上限（20回）に達しました。本文と確認問題は引き続き使えます。',RATE_LIMIT:'質問は1分に3回までです。少し待ってからお試しください。',CAPACITY:'現在は学習の利用数が上限に達しています。時間をおいてお試しください。',ALREADY_ANSWERED:'この問題は回答済みです。',UNCONFIGURED:'AIへの接続設定がありません。本文と確認問題は利用できます。',TIMEOUT:'AIの回答が時間内に届きませんでした。',UPSTREAM:'AIに接続できませんでした。少し待ってからお試しください。',INVALID_OUTPUT:'教材の根拠を含む回答として確認できませんでした。',REFUSED:'今回はAIから回答を取得できませんでした。',INVALID:'入力内容を確認してください。',TOO_LARGE:'入力データが大きすぎます。',FORBIDDEN:'この場所からは利用できません。',INTERNAL:'処理を完了できませんでした。もう一度お試しください。',
};
function failure(code:string,status:number):Response{return localJSON({error:{code,message:messages[code]??messages.INVALID}},status);}
const subscriptions=new WeakMap<InstructorStore,WeakSet<LocalSessionStore>>();
export function createInstructorHandler(options:{environment:string;enabled:boolean;config:{apiKey:string;model:string};sessions?:LocalSessionStore;store?:InstructorStore;now?:()=>number;generate?:typeof generateTutorReply;getLesson?:(id:string)=>PublicLesson|null}):(request:Request)=>Promise<Response>{
 const sessions=options.sessions??getLocalSessionStore(),store=options.store??new InstructorStore();
 const clock=options.now??Date.now,generate=options.generate??generateTutorReply,getLesson=options.getLesson??getPublicLesson;
 let subscribed=subscriptions.get(store);if(!subscribed){subscribed=new WeakSet();subscriptions.set(store,subscribed);}
 if(!subscribed.has(sessions)){sessions.onRevoke(token=>store.revokeOwner(token));subscribed.add(sessions);}
 return async request=>{
  try{
   guardLocalRequest(request,options.environment,options.enabled,request.method!=='GET');
   if(!['GET','POST'].includes(request.method))throw new LocalHttpError(405,'INVALID');
   const token=getSessionToken(request),owner=sessions.resolve(token,clock());
   if(!token||!owner)return failure('UNSELECTED',401);
   if(request.method==='GET'){
    const lesson=getLesson('natural-light');if(!lesson)return failure('NOT_FOUND',404);
    const id=new URL(request.url).searchParams.get('conversationId');
    const conversation=id?store.get(token,id,clock()):undefined;
    if(conversation&&conversation.materialVersion!==lesson.version){store.reset(token,conversation.conversationId,clock());return failure('STALE',409);}
    return localJSON({lesson,questions:getPublicQuestions(lesson.id),aiConfigured:!!(options.config.apiKey.trim()&&options.config.model.trim()),...(conversation?{conversation}:{})});
   }
   const parsed=await readInstructorJSON(request);
   if(!parsed||typeof parsed!=='object'||Array.isArray(parsed))throw new LocalHttpError(400,'INVALID');
   const body=parsed as Record<string,unknown>;
   if(sessions.resolve(token,clock())!==owner)return failure('UNSELECTED',401);
   if(exactKeys(body,['action','materialId'])&&body.action==='start'&&typeof body.materialId==='string'){
    const lesson=getLesson(body.materialId);if(!lesson||lesson.id!==body.materialId)return failure('NOT_FOUND',404);
    return localJSON(store.start(token,lesson,clock()));
   }
   if(!body||typeof body!=='object'||!('conversationId' in body)||typeof body.conversationId!=='string'||body.conversationId.length>64)throw new LocalHttpError(400,'INVALID');
   const c=store.get(token,body.conversationId,clock()),lesson=getLesson(c.materialId);
   if(!lesson||lesson.version!==c.materialVersion){store.reset(token,c.conversationId,clock());return failure('STALE',409);}
   if(exactKeys(body,['action','conversationId'])&&body.action==='reset'){store.reset(token,c.conversationId,clock());return localJSON({reset:true});}
   if(!('sequence' in body)||!Number.isSafeInteger(body.sequence)||Number(body.sequence)<0)throw new LocalHttpError(400,'INVALID');
   const sequence=body.sequence as number;
   if(exactKeys(body,['action','conversationId','sequence','questionId','choice'])&&body.action==='answer'){
    if(typeof body.questionId!=='string'||typeof body.choice!=='number')throw new LocalHttpError(400,'INVALID');
    let result;try{result=gradeQuestion(lesson.id,body.questionId,body.choice);}catch{throw new LocalHttpError(400,'INVALID');}
    return localJSON(store.answer(token,c.conversationId,sequence,body.questionId,result,clock()));
   }
   if(!exactKeys(body,['action','conversationId','sequence','question','sectionId','mode'])||body.action!=='question'||typeof body.question!=='string'||[...body.question].length>1000||!lesson.sections.some(s=>s.id===body.sectionId)||typeof body.mode!=='string'||!['question','simple','example'].includes(body.mode)||(body.mode==='question'&&!body.question.trim()))throw new LocalHttpError(400,'INVALID');
   if(!options.config.apiKey.trim()||!options.config.model.trim())return failure('UNCONFIGURED',503);
   const section=lesson.sections.find(s=>s.id===body.sectionId)!;
   const question=body.mode==='simple'?`「${section.title}」をやさしく説明してください。`:body.mode==='example'?`「${section.title}」の身近な具体例を教えてください。`:`「${section.title}」について：${body.question.trim()}`;
   const lease=store.beginQuestion(token,c.conversationId,sequence,clock());
   try{
    const reply=await generate({lesson:lease.lesson,messages:lease.messages,question},options.config);
    if(sessions.resolve(token,clock())!==owner)return failure('UNSELECTED',401);
    const latest=getLesson(lesson.id);if(!latest||latest.version!==lesson.version){store.reset(token,c.conversationId,clock());return failure('STALE',409);}
    return localJSON(store.finishQuestion(lease,question,reply,clock()));
   }finally{store.cancelQuestion(lease);}
  }catch(error){
   if(error instanceof LocalHttpError)return failure(error.code,error.status);
   if(error instanceof InstructorError)return failure(error.code,statuses[error.code]??500);
   return failure('INTERNAL',500);
  }
 };
}
