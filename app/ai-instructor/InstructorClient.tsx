'use client';
import {useCallback,useEffect,useRef,useState} from 'react';
import Link from 'next/link';
import type {ConversationView,InstructorBootstrap} from '../../shared/ai-instructor';

class RequestError extends Error {constructor(public code:string,message:string){super(message);}}
async function callAPI<T>(body?:unknown,signal?:AbortSignal,conversationId?:string):Promise<T>{
 const response=await fetch('/api/ai-instructor'+(conversationId?'?conversationId='+encodeURIComponent(conversationId):''),{
  method:body===undefined?'GET':'POST',credentials:'same-origin',cache:'no-store',signal,
  ...(body===undefined?{}:{headers:{'Content-Type':'application/json'},body:JSON.stringify(body)}),
 });
 const data=await response.json();
 if(!response.ok)throw new RequestError(data.error?.code??'INTERNAL',data.error?.message??'通信に失敗しました。');
 return data as T;
}
export function InstructorClient(){
 const [bootstrap,setBootstrap]=useState<InstructorBootstrap|null>(null);
 const [conversation,setConversation]=useState<ConversationView|null>(null);
 const [consent,setConsent]=useState(false),[question,setQuestion]=useState(''),[sectionId,setSectionId]=useState('window-light');
 const [choices,setChoices]=useState<Record<string,number>>({});
 const [error,setError]=useState(''),[loginRequired,setLoginRequired]=useState(false),[loading,setLoading]=useState(true),[busy,setBusy]=useState(false);
 const current=useRef<ConversationView|null>(null),epoch=useRef(0),controller=useRef<AbortController|null>(null),pending=useRef(false);
 const publish=useCallback((value:ConversationView|null)=>{current.current=value;setConversation(document.visibilityState==='hidden'?null:value);},[]);
 const fail=useCallback((e:unknown)=>{
  if(e instanceof Error&&e.name==='AbortError')return;
  if(e instanceof RequestError&&['UNSELECTED','NOT_FOUND','EXPIRED','STALE'].includes(e.code)){
   publish(null);setChoices({});setQuestion('');
   if(e.code==='UNSELECTED'){setBootstrap(null);setConsent(false);setLoginRequired(true);}
  }
  setError(e instanceof RequestError?e.message:'通信に失敗しました。接続を確認して、もう一度お試しください。');
 },[publish]);
 const refresh=useCallback(async()=>{
  if(pending.current){
   // Keep the POST alive across tab changes. A focus check may return an older
   // snapshot, so it must never overwrite a reply that has since completed.
   const generation=epoch.current,id=current.current?.conversationId;
   if(!id)return;
   try{
    const data=await callAPI<InstructorBootstrap&{conversation:ConversationView}>(undefined,undefined,id);
    if(generation===epoch.current&&current.current?.conversationId===id&&data.conversation.sequence>=current.current.sequence)publish(data.conversation);
   }catch(e){
    if(generation!==epoch.current)return;
    if(e instanceof RequestError&&['UNSELECTED','NOT_FOUND','EXPIRED','STALE'].includes(e.code)){
     epoch.current++;controller.current?.abort();pending.current=false;setBusy(false);fail(e);
    }
   }
   return;
  }
  const generation=++epoch.current;controller.current?.abort();const abort=new AbortController();controller.current=abort;
  pending.current=false;setBusy(false);setConversation(null);
  try{
   const data=await callAPI<InstructorBootstrap&{conversation?:ConversationView}>(undefined,abort.signal,current.current?.conversationId);
   if(generation!==epoch.current)return;
   setBootstrap(data);setLoginRequired(false);setError('');publish(data.conversation??null);
  }catch(e){if(generation===epoch.current)fail(e);}
  finally{if(generation===epoch.current)setLoading(false);}
 },[fail,publish]);
 useEffect(()=>{
  const initial=setTimeout(()=>void refresh(),0);
  const visible=()=>{if(document.visibilityState==='visible')void refresh();else setConversation(null);};
  const focus=()=>{if(document.visibilityState==='visible')void refresh();};
  document.addEventListener('visibilitychange',visible);window.addEventListener('focus',focus);
  return ()=>{clearTimeout(initial);epoch.current++;controller.current?.abort();document.removeEventListener('visibilitychange',visible);window.removeEventListener('focus',focus);};
 },[refresh]);
 async function mutate(body:unknown,reset=false){
  if(pending.current&&!reset)return;
  const generation=++epoch.current;controller.current?.abort();const abort=new AbortController();controller.current=abort;
  pending.current=true;setBusy(true);setError('');
  try{
   // Recheck ownership before each operation. Start has no conversation to validate yet.
   const before=await callAPI<InstructorBootstrap&{conversation?:ConversationView}>(undefined,abort.signal,current.current?.conversationId);
   if(generation!==epoch.current)return;
   // The authoritative sequence can advance after a transport interruption.
   // Reconcile it before sending the next question or quiz submission.
   const prepared=before.conversation&&body&&typeof body==='object'&&'sequence' in body?{...body,sequence:before.conversation.sequence}:body;
   const result=await callAPI<ConversationView>(prepared,abort.signal);
   if(generation!==epoch.current)return;
   if(reset){publish(null);setChoices({});setQuestion('');return;}
   // Identity could have changed in another tab while the POST was in flight.
   const confirmed=await callAPI<InstructorBootstrap&{conversation:ConversationView}>(undefined,abort.signal,result.conversationId);
   if(generation!==epoch.current)return;
   publish(confirmed.conversation);setQuestion('');
  }catch(e){
   if(generation!==epoch.current||(e instanceof Error&&e.name==='AbortError'))return;
   let recovered=false;
   if(current.current&&!(e instanceof RequestError&&['UNSELECTED','NOT_FOUND','EXPIRED'].includes(e.code))){
    try{
     const data=await callAPI<InstructorBootstrap&{conversation:ConversationView}>(undefined,abort.signal,current.current.conversationId);
     if(generation!==epoch.current)return;
     publish(data.conversation);recovered=true;
    }catch(recoveryError){if(generation===epoch.current)fail(recoveryError);}
   }
   if(generation===epoch.current){if(recovered)setError(e instanceof RequestError?e.message:'通信に失敗しました。もう一度お試しください。');else fail(e);}
  }
  finally{if(generation===epoch.current){pending.current=false;setBusy(false);}}
 }
 function review(id:string){setSectionId(id);const section=document.getElementById(id);section?.focus();section?.scrollIntoView({behavior:'smooth',block:'start'});}
 function ask(mode:'question'|'simple'|'example'){
  if(!conversation)return;
  void mutate({action:'question',conversationId:conversation.conversationId,sequence:conversation.sequence,sectionId,mode,question:mode==='question'?question:''});
 }
 const lesson=bootstrap?.lesson;
 const aiDisabled=busy||!bootstrap?.aiConfigured||!conversation||conversation.remaining===0;
 return <main className="instructor-shell">
  <header className="instructor-header"><Link href="/">みらいふ</Link><nav aria-label="学習ナビゲーション"><Link href="/ai-navi">未来ナビへ</Link><span>AI講師・ローカル検証版</span></nav></header>
  <div className="instructor-intro"><p className="instructor-eyebrow">いっしょに、ひとつずつ。</p><h1>読む。聞く。<br/>わかるを、少しずつ。</h1><p>身近な教材から、自分のペースで。<br/>気になったことはAI講師に聞いてみましょう。</p><span className="instructor-badge">文字チャットで学ぶ サンプル教材</span></div>
  {loading&&<p role="status">教材を読み込んでいます…</p>}
  {error&&<p className="instructor-error" role="alert">{error}</p>}
  {loginRequired&&<section className="instructor-card"><h2>ログインして学習を始めましょう</h2><p>既存の開発用ユーザーでログイン後、このページを開いてください。</p><a className="instructor-link" href="/login">ローカルログインへ</a></section>}
  {!loading&&!lesson&&!loginRequired&&<button type="button" onClick={()=>void refresh()}>もう一度読み込む</button>}
  {lesson&&<>
   <section className="instructor-lesson-title"><div><p className="instructor-eyebrow">写真のきほん / はじめての一歩</p><h2>{lesson.title}</h2><p>新規作成した試験用の本文です。正式な配信教材ではありません。</p></div><ul>{lesson.goals.map(goal=><li key={goal}>{goal}</li>)}</ul></section>
   <div className="instructor-grid"><div className="instructor-reading">
    <section className="instructor-card" aria-label="教材本文"><p className="instructor-eyebrow">LESSON / 教材を読む</p>{lesson.sections.map(section=><article key={section.id} id={section.id} tabIndex={-1} className={sectionId===section.id?'instructor-section is-selected':'instructor-section'}><h3>{section.title}</h3><p>{section.body}</p><button type="button" aria-pressed={sectionId===section.id} onClick={()=>setSectionId(section.id)}>この節について聞く</button></article>)}</section>
    {!conversation&&<section className="instructor-card instructor-start"><h2>準備ができたら、学習を始めましょう</h2><p>AI講師を使うと、教材本文・質問・直近の会話をOpenAIへ送ります。質問には個人情報を入力しないでください。</p><p>会話は一時保存です。30分間の無操作・ログアウト・学習終了・再開始・サーバー再起動で消えます。ページ再読込後の自動復元はありません。</p><label className="instructor-consent"><input type="checkbox" checked={consent} onChange={e=>setConsent(e.target.checked)}/>教材本文と質問をOpenAIへ送信することを確認しました</label><button className="instructor-primary" type="button" disabled={!consent||busy} onClick={()=>void mutate({action:'start',materialId:lesson.id})}>{busy?'準備しています…':'学習を始める'}</button><p className="instructor-caption">本文だけ読む場合は、学習開始の操作は不要です。</p></section>}
    {conversation&&<section className="instructor-card" aria-label="確認問題"><p className="instructor-eyebrow">CHECK / 確かめてみよう</p><h2>学んだことを、3問でおさらい</h2><p>AIの判定を使わない固定問題です。</p>{bootstrap.questions.map((q,index)=>{
     const result=conversation.results[q.id];
     return <fieldset key={q.id} data-testid={`quiz-${q.id}`} className="instructor-quiz"><legend>{index+1}. {q.prompt}</legend>{q.choices.map((choice,i)=><label key={choice}><input type="radio" name={q.id} value={i} checked={choices[q.id]===i} disabled={busy||!!result} onChange={()=>setChoices(previous=>({...previous,[q.id]:i}))}/>{choice}</label>)}{result?<div className="instructor-feedback"><strong>{result.correct?'正解です':'もう一度、教材を見てみましょう'}</strong><p>{result.explanation}</p><button type="button" onClick={()=>review(result.sectionId)}>教材を復習する</button></div>:<button type="button" disabled={busy||choices[q.id]===undefined} onClick={()=>void mutate({action:'answer',conversationId:conversation.conversationId,sequence:conversation.sequence,questionId:q.id,choice:choices[q.id]})}>回答する</button>}</fieldset>;
    })}{Object.keys(conversation.results).length===3&&<div role="status" className="instructor-complete"><h3>確認問題を終了しました</h3><p>正解 {Object.values(conversation.results).filter(r=>r.correct).length} / 3 問</p><p>気になった節を読み直したり、撮り方を実際に比べてみましょう。習得や正式な受講完了を認定するものではありません。</p></div>}</section>}
   </div><aside className="instructor-tutor instructor-card" aria-label="AI講師"><div className="instructor-tutor-heading"><span aria-hidden="true">✦</span><div><p className="instructor-eyebrow">あなたの学びに、よりそう</p><h2>AI講師</h2></div></div>
    <p>教材の内容をもとに、一緒に考えます。回答の根拠となる節も確認できます。</p>
    {!bootstrap.aiConfigured&&<p className="instructor-notice">AIへの接続設定がありません。本文と確認問題は利用できます。</p>}
    {!conversation?<p className="instructor-empty">学習を始めると、ここから質問できます。</p>:<>
     <p className="instructor-caption">このログインで質問できる回数：残り{conversation.remaining}回（1分3回まで）</p>
     {conversation.historyTruncated&&<p className="instructor-notice">直近10メッセージを表示しています。以前の文脈はAIへ送信されません。</p>}
     <div role="log" aria-label="AI講師との会話" aria-live="polite" className="instructor-log">{!conversation.messages.length&&<p className="instructor-empty">「やさしく説明」からでも大丈夫。気になる節を選んでみましょう。</p>}{conversation.messages.map((message,i)=><div key={i} className={`instructor-message ${message.role}`}><span>{message.role==='user'?'あなた':'AI講師'}</span><p>{message.content}</p>{message.sectionIds?.map(id=><button key={id} type="button" onClick={()=>review(id)}>根拠：{lesson.sections.find(s=>s.id===id)?.title}</button>)}</div>)}</div>
     <label className="instructor-label">質問する節<select value={sectionId} onChange={e=>setSectionId(e.target.value)} disabled={busy}>{lesson.sections.map(s=><option key={s.id} value={s.id}>{s.title}</option>)}</select></label>
     <div className="instructor-shortcuts"><button type="button" disabled={aiDisabled} onClick={()=>ask('simple')}>やさしく説明</button><button type="button" disabled={aiDisabled} onClick={()=>ask('example')}>具体例を聞く</button></div>
     <form onSubmit={e=>{e.preventDefault();if(!aiDisabled&&question.trim()&&[...question].length<=1000)ask('question');}}><label className="instructor-label" htmlFor="instructor-question">AI講師への質問</label><textarea id="instructor-question" rows={4} value={question} disabled={busy||!bootstrap.aiConfigured} onChange={e=>setQuestion(e.target.value)} placeholder="光をやわらかくするには？"/><div className="instructor-send"><span className="instructor-caption">{[...question].length} / 1,000文字</span><button type="submit" className="instructor-primary" disabled={aiDisabled||!question.trim()||[...question].length>1000}>{busy?'回答を待っています…':'質問する'}</button></div></form>
     {busy&&<p role="status">処理しています…</p>}
     <button className="instructor-end" type="button" onClick={()=>void mutate({action:'reset',conversationId:conversation.conversationId},true)}>学習を終了する</button>
    </>}
    <p className="instructor-caption">教材にない内容は確認できない旨をお伝えします。AIの回答は誤ることがあります。根拠の本文と合わせて確認してください。</p>
   </aside></div>
  </>}
  <footer className="instructor-footer">みらいふ / 教材伴走型AI講師 · ローカル検証版</footer>
 </main>;
}
