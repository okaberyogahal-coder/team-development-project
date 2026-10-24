'use client';
import {useEffect,useRef,useState} from 'react';
import {useRouter} from 'next/navigation';
import type {LocalDemoState,LocalUserId} from '../../shared/local-demo';
import {LocalClientError,readLocalState,changeLocalState} from '../../shared/local-demo-client';

export function LocalDemoLogin({onLogin}:{onLogin?:(state:LocalDemoState)=>void}){
  const router=useRouter();
  const [state,setState]=useState<LocalDemoState|null>(null);
  const [error,setError]=useState('');const [busy,setBusy]=useState(false);const pending=useRef(false);
  useEffect(()=>{
    const abort=new AbortController();
    readLocalState(abort.signal).then(setState).catch(error=>{
      if(abort.signal.aborted||error instanceof LocalClientError&&error.status===404)return;
      setError(error instanceof Error?error.message:'開発用データを読み込めませんでした。');
    });
    return()=>abort.abort();
  },[]);
  async function login(userId:LocalUserId){
    if(pending.current)return;pending.current=true;setBusy(true);setError('');
    try{const next=await changeLocalState({action:'login',userId});if(onLogin)onLogin(next);else router.push('/ai-navi');}
    catch(error){setError(error instanceof Error?error.message:'開発用ログインに失敗しました。');}
    finally{pending.current=false;setBusy(false);}
  }
  if(!state&&!error)return null;
  return <section className="auth-demo-note local-demo-login" aria-label="JSON共有の開発用ログイン">
    <strong>開発用ユーザーでログイン</strong>
    <p>パスワード不要の架空ユーザー体験です。JSONで共有した履歴を使います。本人確認を行うログインではありません。</p>
    {state&&<div className="local-demo-user-buttons">{state.users.map(user=><button type="button" className="secondary-button" key={user.id} disabled={busy} onClick={()=>login(user.id)}>{user.nickname}で開発用ログイン</button>)}</div>}
    {error&&<p role="alert" className="form-alert">{error}</p>}
  </section>;
}
