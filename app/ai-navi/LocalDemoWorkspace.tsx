'use client';
import {useEffect,useRef,useState,useCallback} from 'react';
import type {LocalDemoState} from '../../shared/local-demo';
import {readLocalState,changeLocalState,LocalClientError} from '../../shared/local-demo-client';
import {DemoClient} from './DemoClient';

export function LocalDemoWorkspace(){
  const [state,setState]=useState<LocalDemoState|null>(null);
  const [error,setError]=useState('');const [busy,setBusy]=useState(false);
  const [invalidation,setInvalidation]=useState(0);const pending=useRef(false);
  const [openMaterial,setOpenMaterial]=useState<string|null>(null);
  const generation=useRef(0);
  const refresh=useCallback(async()=>{const version=generation.current;const next=await readLocalState();if(version!==generation.current)throw new Error('操作が変更されました。');setState(next);return next;},[]);
  useEffect(()=>{const abort=new AbortController();readLocalState(abort.signal).then(setState).catch(error=>{if(!abort.signal.aborted)setError(error instanceof Error?error.message:'保存データを取得できませんでした。');});return()=>abort.abort();},[]);
  async function mutate(body:unknown,opened?:string){
    if(pending.current)return;pending.current=true;generation.current++;setBusy(true);setError('');setInvalidation(value=>value+1);
    try{
      const next=await changeLocalState(body,state?.revision);
      if(next.currentUser?.id!==state?.currentUser?.id)setOpenMaterial(null);
      setState(next);if(opened)setOpenMaterial(opened);
    }catch(error){if(error instanceof LocalClientError&&(error.status===409||error.status===401)){try{await refresh();setOpenMaterial(null);}catch{}}setError(error instanceof Error?error.message:'保存に失敗しました。');}
    finally{pending.current=false;setBusy(false);}
  }
  const user=state?.currentUser;
  const profile=user&&state?{id:user.id,label:user.nickname,description:user.description,history:user.events.flatMap(event=>{
    const material=state.materials.find(item=>item.id===event.materialId);
    return material?[{id:event.id,materialId:event.materialId,kind:event.kind,title:material.title,description:material.description,topics:material.topics}]:[];
  })}:null;
  const material=state?.materials.find(item=>item.id===openMaterial);
  return <>
    <section className="local-demo-toolbar">
      <p><strong>JSON共有・開発用ログイン</strong><span>登録済みテストアカウントの体験版です。</span></p>
      {user&&state&&<div className="local-demo-account"><span data-testid="local-current-user">{user.nickname}で体験中</span>
        <button type="button" disabled={busy} onClick={()=>mutate({action:'logout'})}>ログアウト</button>
      </div>}
      {error&&<p role="alert" className="navi-demo-error">{error}</p>}
    </section>
    {!state&&!error&&<main className="navi-demo"><p role="status">保存したデータを読み込んでいます…</p></main>}
    {state&&!user&&<main className="navi-demo"><section className="navi-demo-panel"><h1>ログインしてはじめる</h1><p>登録済みのテストアカウントでログインしてください。</p><a className="secondary-button" href="/login">ログイン画面へ</a></section></main>}
    {state&&profile&&<DemoClient key={`${user!.id}:${state.revision}:${invalidation}`} profile={profile} materials={state.materials} popularity={state.popularity} revision={state.revision} blocked={busy} refreshState={refresh}/>}
    {state&&user&&<section className="local-demo-library"><details><summary>サンプル教材を開いて履歴を記録</summary>
      <p>教材を開くと閲覧をJSONへ保存します。完了は詳細内のボタンで記録します。同じ教材の再閲覧は1件の日時更新となります。</p>
      <ul>{state.materials.map(item=><li key={item.id}><button type="button" disabled={busy} onClick={()=>mutate({action:'record',materialId:item.id,kind:'view'},item.id)}>{item.title}を開く</button></li>)}</ul>
    </details>
    {material&&<article className="navi-demo-panel local-demo-material" aria-label="サンプル教材の詳細"><h2>{material.title}</h2><p>{material.description}</p><p>実際の教材配信ではなく、履歴保存を試すためのサンプルです。</p><button type="button" className="navi-demo-submit" disabled={busy} onClick={()=>mutate({action:'record',materialId:material.id,kind:'complete'})}>受講完了を記録</button></article>}
    <details className="local-demo-storage"><summary>保存ファイルとGitでの共有</summary><p>架空ユーザーと履歴は data/local-demo/users/*.json、教材は data/local-demo/materials.json に保存します。変更をコミット・プッシュし、他のメンバーがプルすると同じデータを使えます。Git操作・手編集の前はサーバーを停止してください。</p><p>セッションはこのブラウザと起動中のサーバーだけで管理し、Gitへは保存しません。本物の個人情報をJSONに記入しないでください。</p></details>
    </section>}
  </>;
}
