"use client";
import Link from "next/link";
import {useEffect,useRef,useState,type ReactNode} from 'react';

export function WorldFullscreen({children}:{children:ReactNode}) {
 const viewer=useRef<HTMLDivElement>(null);
 const button=useRef<HTMLButtonElement>(null);
 const wasFullscreen=useRef(false);
 const pending=useRef(false);
 const [fullscreen,setFullscreen]=useState(false);
 const [busy,setBusy]=useState(false);
 const [error,setError]=useState('');
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
  <div className="world-viewer-toolbar">
   <h1 className="world-system-name">ミライフ</h1>
   <nav aria-label="ワールドの表示操作"><Link href="/">TOPへ戻る</Link>
   <button ref={button} type="button" onClick={toggle} disabled={busy} aria-pressed={fullscreen} title={fullscreen?'全画面表示を解除（Escでも解除できます）':'ワールドを全画面で表示'}>{fullscreen?'元に戻す':'全画面表示'}</button></nav>
  </div>
  {error&&<p className="unity-error" role="alert">{error}</p>}
  {children}
 </div>;
}
