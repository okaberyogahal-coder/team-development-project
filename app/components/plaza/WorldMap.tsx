'use client';
import {useEffect,useRef,useState} from 'react';
import type {Direction,Participant,RoomId} from '../../../shared/plaza/types';
import {ROOMS,roomById} from '../../../shared/plaza/world';
import {Avatar} from './Avatar';
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
  <svg viewBox="0 0 1000 700" className="plaza-map-svg" role="img" aria-label={`${room.name}のマップ。矢印キーまたはWASDで移動できます。`}>
   <defs><pattern id="grass" width="45" height="45" patternUnits="userSpaceOnUse"><path d="M10 21l2-3 2 3m20 13l2-3 2 3" stroke="#bed4b7" strokeWidth="1.5" fill="none"/></pattern><pattern id="floor" width="60" height="60" patternUnits="userSpaceOnUse"><path d="M60 0H0v60" fill="none" stroke="#e4ded1" strokeWidth="1"/></pattern></defs>
   {roomId==='lobby'?<>
    <rect width="1000" height="700" fill="#d8e6cd"/><rect width="1000" height="700" fill="url(#grass)"/>
    <path d="M435 0h130v700H435zM0 310h1000v90H0z" fill="#f0eade"/>
    <rect x="346" y="228" width="308" height="244" rx="94" fill="#f0eade"/><rect x="364" y="246" width="272" height="208" rx="82" fill="#e7dfcc"/>
    <ellipse cx="500" cy="326" rx="65" ry="35" fill="#d1c9b7"/><ellipse cx="500" cy="315" rx="65" ry="35" fill="#f7f2e6"/><ellipse cx="500" cy="313" rx="52" ry="25" fill="#abd7dc"/>
    <ellipse cx="500" cy="306" rx="25" ry="13" fill="#d0e7e6"/><path d="M500 302v-25" stroke="#fbfffb" strokeWidth="4"/><circle cx="500" cy="275" r="5" fill="#fbfffb"/>
    <rect x="260" y="75" width="150" height="95" rx="16" fill="#b9ceb0"/><rect x="590" y="75" width="150" height="95" rx="16" fill="#b9ceb0"/>
    <Tree x={290} y={125}/><Tree x={375} y={130} size={.85}/><Tree x={625} y={130} size={.85}/><Tree x={710} y={125}/>
    <Tree x={55} y={95}/><Tree x={940} y={100}/><Tree x={325} y={610}/><Tree x={675} y={610}/>
    <Bench x={365} y={393}/><Bench x={570} y={393}/><Bench x={460} y={610}/>
    <Plant x={415} y={225}/><Plant x={585} y={225}/><Plant x={400} y={485}/><Plant x={600} y={485}/>
    {houses.map(h=>{const r=roomById(h.id);return <g key={h.id} transform={`translate(${h.x} ${h.y})`}>
     <rect x="6" y="17" width="204" height="123" rx="14" fill="#7c9b76" opacity=".15"/><rect width="204" height="125" rx="14" fill="#fff9ed"/>
     <rect width="204" height="36" rx="14" fill={r.color}/><rect y="23" width="204" height="13" fill={r.color}/><path d="M0 36h204" stroke="#fff" opacity=".5" strokeWidth="2"/>
     <rect x="17" y="52" width="48" height="37" rx="6" fill="#c5dfe0"/><path d="M41 52v37m-24-19h48" stroke="#fff9ed" strokeWidth="3"/>
     <rect x="139" y="52" width="48" height="37" rx="6" fill="#c5dfe0"/><path d="M163 52v37m-24-19h48" stroke="#fff9ed" strokeWidth="3"/>
     <rect x="82" y="69" width="40" height="56" rx="6" fill="#bf9d7a"/><circle cx="114" cy="100" r="2" fill="#fff9ed"/>
     <text x="102" y="25" textAnchor="middle" fill="#fff" fontSize="14" fontWeight="bold">{r.name}</text>
    </g>;})}
   </>:<>
    <rect width="1000" height="700" fill="#eee8da"/><rect width="1000" height="700" fill="url(#floor)"/>
    <rect x="36" y="100" width="928" height="22" rx="8" fill={room.color}/><rect x="36" y="122" width="928" height="60" rx="8" fill="#fffaf0"/>
    {[150,370,590,810].map(x=><g key={x}><rect x={x} y="129" width="75" height="38" rx="6" fill="#c6dfe2"/><path d={`M${x+37} 129v38`} stroke="#fff" strokeWidth="4"/></g>)}
    <rect x="300" y="260" width="400" height="235" rx="58" fill={room.color} opacity=".15"/>
    <ellipse cx="500" cy="350" rx="98" ry="54" fill="#c4a380"/><ellipse cx="500" cy="340" rx="98" ry="54" fill="#e3c5a1"/><ellipse cx="500" cy="334" rx="35" ry="19" fill="#fff7e7"/>
    <Plant x={500} y={324}/>
    {[{x:365,y:310},{x:365,y:380},{x:609,y:310},{x:609,y:380}].map((p,i)=><g key={i}><rect x={p.x} y={p.y} width="30" height="32" rx="9" fill={room.color}/><rect x={p.x+3} y={p.y-6} width="24" height="12" rx="5" fill={room.color}/></g>)}
    <Plant x={90} y={195}/><Plant x={910} y={195}/><Plant x={100} y={585}/><Plant x={900} y={585}/>
    <rect x="65" y="280" width="90" height="160" rx="12" fill={room.color} opacity=".65"/><rect x="85" y="290" width="45" height="140" rx="9" fill={room.color}/>
    <rect x="835" y="280" width="90" height="160" rx="12" fill={room.color} opacity=".65"/><rect x="865" y="290" width="45" height="140" rx="9" fill={room.color}/>
    <rect x="415" y="614" width="170" height="45" rx="8" fill="#d0b38f"/><text x="500" y="642" textAnchor="middle" fill="#7d684f" fontSize="15">ロビーへ戻る ↓</text>
   </>}
  </svg>
  {roomId==='lobby' && houses.map(h=><button key={h.id} className="plaza-map-door" style={{left:`${(h.x+102)/10}%`,top:`${(h.y+135)/7}%`}} onClick={()=>onRoom(h.id)} disabled={disabled} aria-label={`${roomById(h.id).name}に入る`}>入る <span>→</span></button>)}
  {roomId!=='lobby' && <button className="plaza-map-door" style={{left:'50%',top:'91%'}} onClick={()=>onRoom('lobby')} disabled={disabled}>ロビーへ →</button>}
  {participants.map(p=><div key={p.id} className={`plaza-person ${p.id===selfId?'is-self':''}`} style={{left:`${p.x/10}%`,top:`${p.y/7}%`,opacity:p.connected?1:.45}}><span>{p.name}{p.id===selfId?'（あなた）':''}</span><Avatar avatar={p.avatar} size={43}/></div>)}
  <div className="plaza-map-tip">{disabled?'接続を待っています…':<>移動 <kbd>↑</kbd><kbd>←</kbd><kbd>↓</kbd><kbd>→</kbd><span>または WASD</span></>}</div>
  {help && <div className="plaza-help-popover"><strong>広場の歩き方</strong><p>矢印キー・WASDで移動します。「入る」または左の部屋一覧から部屋へ移れます。カメラ・マイクは自分でオンにしてください。</p><button onClick={()=>setHelp(false)}>閉じる</button></div>}
 </div>;
}
