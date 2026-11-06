"use client";
import {useEffect,useRef,useState} from 'react';
import {resolveWorldAvatar} from '../../shared/world-avatar';

export function UnityExperience({mode}:{mode:'avatar'|'world';accountProfile?:{id:string;nickname:string}|null}) {
 const frame=useRef<HTMLIFrameElement>(null);
 const [error,setError]=useState('');
 useEffect(()=>{
  let ready=false,ownerId:string|null=null,last='';
  const initialize=()=>{
   if(!ready)return;
   try{
    const profile=resolveWorldAvatar(localStorage,ownerId);
    const key=JSON.stringify([ownerId,profile?.appearance]);
    if(key===last)return;
    frame.current?.contentWindow?.postMessage({type:'melife:initialize',mode,profile},location.origin);
    last=key;
   }catch{setError('このブラウザに保存されたアバターを読み込めません。保存設定を確認してください。');}
  };
  const receive=(event:MessageEvent)=>{
   if(event.origin!==location.origin||event.source!==frame.current?.contentWindow)return;
   if(event.data?.type==='melife:ready'){ready=true;last='';initialize();}
   if(event.data?.type==='melife:session-header'){
    ownerId=event.data.authenticated===true&&typeof event.data.ownerId==='string'?event.data.ownerId:null;
    initialize();
   }
   if(event.data?.type==='melife:error')setError('アバターまたはワールドを表示できません。ページを再読み込みしてください。');
  };
  window.addEventListener('message',receive);
  return()=>window.removeEventListener('message',receive);
 },[mode]);
 return <>
  {error&&<p role="alert" className="unity-error">{error}</p>}
  <iframe key={mode} ref={frame} className="unity-frame" title={mode==='avatar'?'3Dアバター作成':'ミライフ Unityワールド'} src={`/unity/FutureCity/index.html${mode==='avatar'?'?experience=avatar':''}`} allow="fullscreen; autoplay" allowFullScreen />
 </>;
}
