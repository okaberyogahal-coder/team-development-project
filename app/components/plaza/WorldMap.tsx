'use client';
import {useEffect,useRef,useState} from 'react';
import type {Direction,Participant,RoomId} from '../../../shared/plaza/types';
import {ROOMS,roomById} from '../../../shared/plaza/world';
import dynamic from 'next/dynamic';
const WorldCanvas=dynamic(()=>import('./world3d/WorldCanvas'),{ssr:false,loading:()=> <div className="plaza-world-loading">3D広場を準備しています…</div>});
function Tree({x,y,size=1}:{x:number;y:number;size?:number}){return <g transform={`translate(${x} ${y}) scale(${size})`}><ellipse cy="26" rx="26" ry="9" fill="#6b9d79" opacity=".18"/><rect x="-4" y="-8" width="8" height="37" rx="3" fill="#ae8864"/><ellipse cy="-19" rx="28" ry="31" fill="#86b79b"/><ellipse cx="-12" cy="-16" rx="19" ry="24" fill="#92c3a3"/><ellipse cx="8" cy="-27" rx="17" ry="22" fill="#a4ceb0"/></g>;}
function Plant({x,y}:{x:number;y:number}){return <g transform={`translate(${x} ${y})`}><rect x="-12" y="0" width="24" height="14" rx="4" fill="#d9b091"/><ellipse cy="-7" rx="14" ry="12" fill="#76af87"/><path d="M-7-9l7 8 7-11" stroke="#a4cfac" strokeWidth="3" fill="none"/></g>;}
function Bench({x,y}:{x:number;y:number}){return <g transform={`translate(${x} ${y})`}><rect width="65" height="14" rx="4" fill="#c59d78"/><path d="M5 7h55M8 14v11m49-11v11" stroke="#a28669" strokeWidth="5"/></g>;}
const houses=[{id:'cafe' as RoomId,x:90,y:195},{id:'hobby' as RoomId,x:700,y:195},{id:'learning' as RoomId,x:90,y:460},{id:'sharing' as RoomId,x:700,y:460}];
export function WorldMap({roomId,participants,selfId,onMove,onRoom,disabled}:{roomId:RoomId;participants:Participant[];selfId:string;onMove:(d:Direction)=>void;onRoom:(r:RoomId)=>void;disabled:boolean}){
 const keys=useRef(new Set<string>());const [help,setHelp]=useState(false);
 useEffect(()=>{
  const stopped=()=>{keys.current.clear();onMove({x:0,y:0});};
  const update=()=>onMove({x:Number(keys.current.has('arrowright')||keys.current.has('d'))-Number(keys.current.has('arrowleft')||keys.current.has('a')),y:Number(keys.current.has('arrowdown')||keys.current.has('s'))-Number(keys.current.has('arrowup')||keys.current.has('w'))});
  const down=(e:KeyboardEvent)=>{
   const target=e.target as HTMLElement;const key=e.key.toLowerCase();if(target.closest('input,textarea,select,[contenteditable="true"]'))return;
   if(!['arrowup','arrowdown','arrowleft','arrowright','w','a','s','d'].includes(key) || disabled)return;
   e.preventDefault();if(keys.current.has(key))return;keys.current.add(key);update();
  };
  const up=(e:KeyboardEvent)=>{if(keys.current.delete(e.key.toLowerCase()))update();};
  window.addEventListener('keydown',down);window.addEventListener('keyup',up);window.addEventListener('blur',stopped);document.addEventListener('visibilitychange',stopped);
  const focus=()=>{if(document.activeElement?.matches('input,textarea,select,[contenteditable="true"]'))stopped();};document.addEventListener('focusin',focus);
  return ()=>{stopped();window.removeEventListener('keydown',down);window.removeEventListener('keyup',up);window.removeEventListener('blur',stopped);document.removeEventListener('visibilitychange',stopped);document.removeEventListener('focusin',focus);};
 },[onMove,disabled,roomId]);
 const room=roomById(roomId);
 return <div className={`plaza-map ${disabled?'plaza-map-disabled':''}`}>
  <div className="plaza-map-badge"><span>{room.icon}</span><div><strong>{room.name}</strong><small>{roomId==='lobby'?'待ち合わせにぴったりの場所':'この部屋の人だけで会話できます'}</small></div></div>
  <button className="plaza-help" aria-label="操作説明" onClick={()=>setHelp(!help)}>?</button>
  <WorldCanvas roomId={roomId} participants={participants} selfId={selfId} onRoom={onRoom} disabled={disabled} onFailure={()=>{}}/>
  <div className="plaza-map-tip">{disabled?'接続を待っています…':<>移動 <kbd>↑</kbd><kbd>←</kbd><kbd>↓</kbd><kbd>→</kbd><span>または WASD</span></>}</div>
  {help && <div className="plaza-help-popover"><strong>広場の歩き方</strong><p>矢印キー・WASDで移動します。「入る」または左の部屋一覧から部屋へ移れます。カメラ・マイクは自分でオンにしてください。</p><button onClick={()=>setHelp(false)}>閉じる</button></div>}
 </div>;
}
