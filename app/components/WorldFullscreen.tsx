"use client";
import Link from "next/link";
import Image from "next/image";
import {Logo} from "./Logo";
import {useEffect,useRef,useState,type ReactNode} from 'react';

type SessionHeader={authenticated:boolean;state:string;status:string;avatarId:string|null;busy:boolean;canRetry:boolean;error:string};
const initialHeader:SessionHeader={authenticated:false,state:'loading',status:'ログイン状態を確認中…',avatarId:null,busy:false,canRetry:false,error:''};
const avatars=['sprout','cat','bear','rabbit','penguin','fox'];

export function WorldFullscreen({children}:{children:ReactNode}) {
 const viewer=useRef<HTMLDivElement>(null);
 const button=useRef<HTMLButtonElement>(null);
 const wasFullscreen=useRef(false);
 const pending=useRef(false);
 const [fullscreen,setFullscreen]=useState(false);
 const [busy,setBusy]=useState(false);
 const [error,setError]=useState('');
 const [session,setSession]=useState(initialHeader);
 function sessionCommand(action:'logout'|'retry'){
  viewer.current?.querySelector('iframe')?.contentWindow?.postMessage({type:'melife:host-session-command',action},window.location.origin);
 }
 useEffect(()=>{
  const root=viewer.current;
  function connect(){
   root?.querySelector('iframe')?.contentWindow?.postMessage({type:'melife:host-session-header'},window.location.origin);
  }
  function receive(event:MessageEvent){
   if(event.origin!==window.location.origin||event.source!==root?.querySelector('iframe')?.contentWindow)return;
   const data=event.data;
   if(data?.type==='melife:ready'){connect();return;}
   if(data?.type!=='melife:session-header'||typeof data.authenticated!=='boolean'||typeof data.status!=='string'||data.status.length>80||typeof data.busy!=='boolean'||typeof data.canRetry!=='boolean'||typeof data.error!=='string'||data.error.length>200||!['loading','authenticated','guest','unavailable'].includes(data.state))return;
   setSession({authenticated:data.authenticated,state:data.state,status:data.status,avatarId:avatars.includes(data.avatarId)?data.avatarId:null,busy:data.busy,canRetry:data.canRetry,error:data.error});
  }
  function loaded(event:Event){if(event.target===root?.querySelector('iframe')){setSession(initialHeader);connect();}}
  window.addEventListener('message',receive);
  root?.addEventListener('load',loaded,true);
  connect();
  return()=>{window.removeEventListener('message',receive);root?.removeEventListener('load',loaded,true);};
 },[]);
 useEffect(()=>{
  function changed(){
   const active=document.fullscreenElement===viewer.current;
   if(wasFullscreen.current&&!active)button.current?.focus();
   wasFullscreen.current=active;
   setFullscreen(active);
  }
  document.addEventListener('fullscreenchange',changed);
  return()=>document.removeEventListener('fullscreenchange',changed);
 },[]);
 async function toggle(){
  if(pending.current||!viewer.current)return;
  setError('');
  if(typeof viewer.current.requestFullscreen!=='function'){
   setError('このブラウザは全画面表示に対応していません。');return;
  }
  pending.current=true;setBusy(true);
  try{
   if(document.fullscreenElement===viewer.current)await document.exitFullscreen();
   else {
    await viewer.current.requestFullscreen();
    const frame=viewer.current.querySelector('iframe');
    frame?.contentDocument?.getElementById('unity-canvas')?.focus({preventScroll:true});
   }
  }catch{setError('全画面表示を切り替えられませんでした。もう一度お試しください。');}
  finally{pending.current=false;setBusy(false);}
 }
 return <div ref={viewer} className="world-viewer">
  <header className="world-viewer-toolbar">
   <h1 className="sr-only">ミライフ ワールド</h1>
   <Logo priority/>
   <div className="world-session-name" role="status" title={session.status}>
    {session.avatarId&&<Image src={`/images/icons/${session.avatarId}.svg`} alt="" width={24} height={24} unoptimized/>}
    <span>{session.status}</span>
   </div>
   <nav aria-label="ワールドの操作">
    {session.authenticated&&<button type="button" className="world-header-control" disabled={session.busy} onClick={()=>sessionCommand('logout')}>{session.busy?'ログアウト中…':'ログアウト'}</button>}
    <Link className="world-header-control" href="/">TOPへ戻る</Link>
    <Link className="world-header-control" href={session.authenticated?'/account':'/login'}>{session.authenticated?'マイページ':'ログインする'}</Link>
    <button className="world-header-control world-fullscreen-control" ref={button} type="button" onClick={toggle} disabled={busy} aria-pressed={fullscreen} title={fullscreen?'全画面表示を解除（Escでも解除できます）':'ワールドを全画面で表示'}>{fullscreen?'元に戻す':'全画面表示'}</button>
   </nav>
  </header>
  {session.error&&<div className="world-session-error"><p role="alert">{session.error}</p>{session.canRetry&&<button type="button" onClick={()=>sessionCommand('retry')}>再確認</button>}</div>}
  {error&&<p className="unity-error" role="alert">{error}</p>}
  {children}
 </div>;
}
