"use client";
import { useEffect, useRef, useState } from 'react';
import { useRouter } from 'next/navigation';
import { DEFAULT_AVATAR, createDemoProfile, saveDemoProfile, readDemoProfile, updateDemoAvatar, type DemoProfile } from '../../shared/demo-profile';

export function UnityExperience({ mode, accountProfile }: { mode: 'avatar' | 'world'; accountProfile?: {id:string;nickname:string} | null }) {
 const router = useRouter();
 const frame = useRef<HTMLIFrameElement>(null);
 const [profile, setProfile] = useState<DemoProfile | null>(null);
 const [checked, setChecked] = useState(false);
 const [error, setError] = useState('');
 useEffect(() => {
  let saved: DemoProfile | null;
  try {
   saved = readDemoProfile(window.localStorage);
   if (accountProfile) {
    saved = saved?.id === accountProfile.id ? {...saved,nickname:accountProfile.nickname} : createDemoProfile(accountProfile.nickname,accountProfile.id);
    saveDemoProfile(window.localStorage,saved);
   }
  }
  catch { setError('このブラウザの保存機能を利用できません。ブラウザの設定を確認してください。'); return; }
  if (mode === 'avatar' && !saved) { router.replace('/signup'); return; }
  if (mode === 'world' && saved && !saved.appearance) { router.replace('/avatar'); return; }
  setProfile(saved); setChecked(true);
  function receive(event: MessageEvent) {
   if (event.origin !== window.location.origin || event.source !== frame.current?.contentWindow) return;
   const message = event.data;
   if (message?.type === 'melife:ready') {
    frame.current?.contentWindow?.postMessage({type:'melife:initialize',mode,profile:saved ? {...saved,appearance:saved.appearance ?? DEFAULT_AVATAR} : null},window.location.origin);
   } else if (message?.type === 'melife:appearance' || message?.type === 'melife:complete') {
    try {
     if (!updateDemoAvatar(window.localStorage,message)) { setError('アバターを保存できませんでした。もう一度作成してください。'); return; }
     setProfile(readDemoProfile(window.localStorage));
     setError('');
     if (message.type === 'melife:complete') router.push('/plaza');
    } catch { setError('このブラウザに保存できません。ブラウザの保存設定を確認してください。'); }
   }
  }
  window.addEventListener('message',receive);
  return () => window.removeEventListener('message',receive);
 },[mode,router,accountProfile?.id,accountProfile?.nickname]);
 return <>
  {mode === 'avatar' && <div className="unity-onboarding avatar-setup-onboarding"><strong>{profile?.nickname}さんのアバターを作成</strong><span>服・表情・髪型・小物を選び、画面内の「このアバターで入場」を押してください。</span></div>}
  {mode === 'world' && profile && <div className="unity-onboarding"><strong>{profile.nickname}さんの体験プロフィール</strong><a href="/avatar">アバターを変更</a></div>}
  {mode === 'world' && checked && !profile && <div className="unity-onboarding"><span>ゲストで体験中です。</span><a href="/signup">登録体験からアバターを作る</a></div>}
  {error && <p role="alert" className="unity-error">{error}</p>}
  {/* Keep the iframe in initial HTML; delayed insertion can stall navigation on reload. */}
  <iframe key={mode} ref={frame} className="unity-frame" title={mode === 'avatar' ? '3Dアバター作成' : 'ミライフ Unityワールド'} src={`/unity/FutureCity/index.html${mode === 'avatar' ? '?experience=avatar' : ''}`} allow="fullscreen; autoplay" allowFullScreen />
 </>;
}
