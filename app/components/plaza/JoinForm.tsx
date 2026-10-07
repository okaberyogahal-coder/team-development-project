'use client';
import {useEffect,useRef,useState} from 'react';
import {AVATARS} from '../../../shared/plaza/world';
import type {AvatarId} from '../../../shared/plaza/types';
import {Avatar} from './Avatar';
export function JoinForm({onJoin,status,error,onCancel}:{onJoin:(name:string,avatar:AvatarId)=>void;status:string;error:string|null;onCancel:()=>void}){
 const [ready,setReady]=useState(false);
 useEffect(()=>setReady(true),[]);
 const [name,setName]=useState('');const [avatar,setAvatar]=useState<AvatarId>('coral');const [device,setDevice]=useState('カメラ・マイクは入場後もオフです');const [preview,setPreview]=useState(false);const [checking,setChecking]=useState(false);
 const video=useRef<HTMLVideoElement>(null);const stream=useRef<MediaStream|null>(null);const checkGeneration=useRef(0);
 const stop=()=>{checkGeneration.current++;stream.current?.getTracks().forEach(t=>t.stop());stream.current=null;if(video.current)video.current.srcObject=null;setPreview(false);setChecking(false);};
 useEffect(()=>()=>{checkGeneration.current++;stream.current?.getTracks().forEach(t=>t.stop());},[]);
 async function check(){
  const generation=++checkGeneration.current;setChecking(true);
  try{
   const media=await navigator.mediaDevices.getUserMedia({video:true,audio:true});
   if(generation!==checkGeneration.current){media.getTracks().forEach(t=>t.stop());return;}
   stream.current?.getTracks().forEach(t=>t.stop());stream.current=media;if(video.current)video.current.srcObject=media;
   setPreview(true);setDevice('確認できました。映像・音声は他の人へ送られません。');
  }catch{if(generation===checkGeneration.current)setDevice('機器を確認できませんでした。カメラ・マイクなしでも入場できます。');}
  finally{if(generation===checkGeneration.current)setChecking(false);}
 }
 const busy=status==='connecting' || status==='reconnecting';
 return <div className="plaza-entry-grid">
  <div className="plaza-welcome">
   <span className="plaza-eyebrow">MIRAI LIFE COMMUNITY</span>
   <h1>ここで会おう。<br/><span>新しい誰かと。</span></h1>
   <p>好きなこと、知っていること。<br/>小さな会話から、次の挑戦がはじまります。</p>
   <div className="plaza-welcome-notes"><span>🌿 自分のペースで歩こう</span><span>☕ 部屋に入っておしゃべり</span><span>🎥 カメラはオンでもオフでも</span></div>
   <div className="plaza-entry-note"><span className="plaza-dot"/>PCテスト版 · ロビー＋4つの交流ルーム</div>
  </div>
  <form className="plaza-join-card" onSubmit={e=>{e.preventDefault();stop();onJoin(name.trim(),avatar);}}>
   <div className="plaza-card-heading"><span>01 / JOIN THE PLAZA</span><h2>あなたらしく、入場しよう</h2><p>名前とアバターを選ぶだけで参加できます。</p></div>
   <div className="plaza-avatar-preview"><Avatar avatar={avatar} size={88}/><div><strong>{name.trim() || 'あなた'}</strong><span>今日のアバター</span></div><span className="plaza-small-tag">GUEST</span></div>
   <label className="plaza-label" htmlFor="plaza-name">広場での名前</label>
   <input id="plaza-name" className="plaza-input" value={name} onChange={e=>setName(e.target.value)} maxLength={20} required placeholder="例：はな、たろう" autoComplete="nickname" disabled={busy || !ready}/>
   <fieldset className="plaza-avatar-field"><legend className="plaza-label">アバターを選ぶ</legend><div className="plaza-avatar-options">{AVATARS.map(a=><button type="button" key={a.id} className={avatar===a.id?'selected':''} onClick={()=>setAvatar(a.id)} aria-label={a.label} aria-pressed={avatar===a.id} disabled={busy || !ready}><Avatar avatar={a.id} size={34}/></button>)}</div></fieldset>
   <div className="plaza-device-check"><div><strong>カメラ・マイクの確認</strong><span>確認は任意です。音声だけでも参加できます。</span></div><button type="button" onClick={preview?stop:check} disabled={busy || checking}>{preview?'終了':checking?'確認中…':'確認する'}</button><video ref={video} muted autoPlay playsInline className={preview?'':'hidden'}/><p>{device}</p></div>
   {error && <p className="plaza-error" role="alert">{error}</p>}
   <button className="plaza-primary plaza-join-button" disabled={busy || !ready || !name.trim()}>{busy?'広場に接続しています…':'広場に入る'} <span aria-hidden="true">→</span></button>
   {busy && <button type="button" className="plaza-text-button" onClick={onCancel}>接続を中止する</button>}
   <p className="plaza-privacy-note">入室してもカメラ・マイクは自動でオンになりません。</p>
  </form>
 </div>;
}
