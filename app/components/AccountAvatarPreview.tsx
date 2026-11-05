"use client";

import {useEffect, useRef, useState} from 'react';
import Link from 'next/link';
import {resolveAccountAvatar} from '../../shared/account-avatar';
import type {DemoProfile} from '../../shared/demo-profile';

type PreviewState = 'loading' | 'ready' | 'missing' | 'incomplete' | 'error';

export function AccountAvatarPreview({ownerId,nickname,gender}:{ownerId:string;nickname:string;gender:string|null}) {
 const frame = useRef<HTMLIFrameElement>(null);
 const [state,setState] = useState<PreviewState>('loading');
 const [displayProfile,setDisplayProfile] = useState<DemoProfile|null>(null);
 const [attempt,setAttempt] = useState(0);
 // The saved avatar is browser-only. Resolve it after hydration, before mounting
 // the iframe, so its first ready message cannot precede the event listener.
 /* eslint-disable react-hooks/set-state-in-effect */
 useEffect(()=>{
  let timer: ReturnType<typeof setTimeout>;
  if(gender!=='male'&&gender!=='female'){setDisplayProfile(null);setState('incomplete');return;}
  let profile: DemoProfile | null;
  try { profile = resolveAccountAvatar(window.localStorage,ownerId,nickname); }
  catch { setDisplayProfile(null); setState('error'); return; }
  if (!profile) { setDisplayProfile(null); setState('missing'); return; }
  setDisplayProfile(profile);
  setState('loading');
  timer = setTimeout(()=>setState('error'),90000);
  function receive(event:MessageEvent) {
   if (event.origin !== window.location.origin || event.source !== frame.current?.contentWindow) return;
   if (event.data?.type === 'melife:ready') {
    frame.current?.contentWindow?.postMessage({type:'melife:initialize',mode:'profile',profile:{...profile,gender}},window.location.origin);
   } else if (event.data?.type === 'melife:profile-ready') {
    clearTimeout(timer); setState('ready');
   } else if (event.data?.type === 'melife:error') {
    clearTimeout(timer); setState('error');
   }
  }
  window.addEventListener('message',receive);
  return ()=>{clearTimeout(timer);window.removeEventListener('message',receive);};
 },[ownerId,nickname,gender,attempt]);
 /* eslint-enable react-hooks/set-state-in-effect */
 return <div className="account-avatar-stage" aria-busy={state==='loading'}>
  {displayProfile?.id===ownerId&&(state==='loading'||state==='ready')&&<iframe key={`${ownerId}-${attempt}`} ref={frame} title="現在の3Dアバター" src="/unity/FutureCity/index.html?experience=profile" onError={()=>setState('error')} />}
  {state==='loading'&&<div className="account-avatar-placeholder" role="status"><span className="account-avatar-loader" aria-hidden="true"/><strong>アバターを読み込み中</strong><p>初回は少し時間がかかります。</p></div>}
  {state==='missing'&&<div className="account-avatar-placeholder"><span className="account-avatar-empty" aria-hidden="true">＋</span><strong>あなたらしい姿を、ここに。</strong><p>このブラウザには、設定済みの<br/>アバターがありません。</p><Link href="/avatar">アバターを作成する →</Link></div>}
  {state==='incomplete'&&<div className="account-avatar-placeholder"><strong>プロフィールを完成させましょう。</strong><p>性別の設定後に、あなたの<br/>アバターを表示できます。</p><Link href="/account/edit">プロフィールを編集する →</Link></div>}
  {state==='error'&&<div className="account-avatar-placeholder"><strong role="alert">アバターを表示できませんでした。</strong><p>通信状態とブラウザの保存設定を<br/>確認して、もう一度お試しください。</p><button type="button" onClick={()=>{setDisplayProfile(null);setState('loading');setAttempt(value=>value+1);}}>もう一度読み込む ↻</button></div>}
 </div>;
}
