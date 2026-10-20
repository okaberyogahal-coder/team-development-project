'use client';
import {Icon} from "../../Icon";
import {useEffect,useRef} from 'react';
import type {Participant,RoomId} from '../../../../shared/plaza/types';
import {ROOMS,roomById} from '../../../../shared/plaza/world';
import {createRuntime,Frame} from './runtime';

export type WorldCanvasProps={roomId:RoomId;participants:Participant[];selfId:string;onRoom:(room:RoomId)=>void;disabled:boolean;onFailure:()=>void};
export default function WorldCanvas({roomId,participants,selfId,onRoom,disabled,onFailure}:WorldCanvasProps){
 const host=useRef<HTMLDivElement>(null);const runtime=useRef<ReturnType<typeof createRuntime>|null>(null);
 const latest=useRef({participants,selfId,onFailure});latest.current={participants,selfId,onFailure};
 const labels=useRef(new Map<string,HTMLDivElement>());const doors=useRef(new Map<RoomId,HTMLButtonElement>());
 useEffect(()=>{
  if(!host.current)return;
  const frame=(value:Frame)=>{
   for(const p of value.people){const element=labels.current.get(p.id);if(element){element.style.left=`${p.x}px`;element.style.top=`${p.y}px`;element.style.visibility='visible';element.dataset.worldX=p.worldX.toFixed(2);element.dataset.worldY=p.worldY.toFixed(2);}}
   for(const d of value.doors){const element=doors.current.get(d.roomId);if(element){element.style.left=`${d.x}px`;element.style.top=`${d.y}px`;element.style.visibility='visible';}}
  };
  try{runtime.current=createRuntime(host.current,roomId,frame,()=>latest.current.onFailure());runtime.current.update(latest.current.participants,latest.current.selfId);}
  catch{latest.current.onFailure();}
  return()=>{runtime.current?.dispose();runtime.current=null;};
 },[roomId]);
 useEffect(()=>{runtime.current?.update(participants,selfId);},[participants,selfId]);
 const destinations=roomId==='lobby'?ROOMS.filter(r=>r.id!=='lobby'):[roomById('lobby')];
 return <div className="plaza-world3d">
  <div className="plaza-world3d-canvas" ref={host}/>
  {participants.filter(p=>p.roomId===roomId).map(p=><div key={p.id} ref={element=>{if(element)labels.current.set(p.id,element);else labels.current.delete(p.id);}} className={`plaza-person plaza-person-3d ${p.id===selfId?'is-self':''}`} style={{visibility:'hidden',opacity:p.connected?1:0.45}}><span>{p.name}{p.id===selfId?'（あなた）':''}</span></div>)}
  {destinations.map(r=><button key={r.id} ref={element=>{if(element)doors.current.set(r.id,element);else doors.current.delete(r.id);}} className="plaza-map-door plaza-door-3d" style={{visibility:'hidden'}} aria-label={r.id==='lobby'?'ロビーへ →':`${r.name}に入る`} disabled={disabled} onClick={()=>onRoom(r.id)}>{r.id==='lobby'?'ロビーへ →':<><Icon name={r.icon} /> {r.name}<span>入る →</span></>}</button>)}
 </div>;
}
