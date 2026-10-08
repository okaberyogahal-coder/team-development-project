'use client';
import {useState} from 'react';
import dynamic from 'next/dynamic';
import type {Direction,Participant,RoomId} from '../../../shared/plaza/types';
import {roomById} from '../../../shared/plaza/world';
import {WorldMap2D} from './WorldMap2D';
import {useWorldMovement} from './useWorldMovement';
import {WorldBoundary} from './world3d/WorldBoundary';
const WorldCanvas=dynamic(()=>import('./world3d/WorldCanvas'),{ssr:false,loading:()=> <div className="plaza-world-loading" role="status">3D広場を準備しています…</div>});

export function WorldMap({roomId,participants,selfId,onMove,onRoom,disabled}:{roomId:RoomId;participants:Participant[];selfId:string;onMove:(d:Direction)=>void;onRoom:(r:RoomId)=>void;disabled:boolean}){
 const [help,setHelp]=useState(false);const [failed,setFailed]=useState(false);const [use2D,setUse2D]=useState(false);
 useWorldMovement(onMove,disabled,roomId);
 const room=roomById(roomId);
 return <div className={`plaza-map ${disabled?'plaza-map-disabled':''}`}>
  <div className="plaza-map-badge"><span>{room.icon}</span><div><strong>{room.name}</strong><small>{roomId==='lobby'?'待ち合わせにぴったりの場所':'この部屋の人だけで会話できます'}</small></div></div>
  <button className="plaza-help" aria-label="操作説明" onClick={()=>setHelp(!help)}>?</button>
  {use2D?<WorldMap2D roomId={roomId} participants={participants} selfId={selfId} onRoom={onRoom} disabled={disabled}/>:failed?<div className="plaza-world-failure" role="alert"><span>🌿</span><strong>この環境では3D表示を利用できません。</strong><p>2Dの広場でも、移動やおしゃべりを楽しめます。</p><button onClick={()=>setUse2D(true)}>2Dで表示</button></div>:<WorldBoundary onFailure={()=>setFailed(true)}><WorldCanvas roomId={roomId} participants={participants} selfId={selfId} onRoom={onRoom} disabled={disabled} onFailure={()=>setFailed(true)}/></WorldBoundary>}
  <div className="plaza-map-tip">{disabled?'接続を待っています…':<>移動 <kbd>↑</kbd><kbd>←</kbd><kbd>↓</kbd><kbd>→</kbd><span>または WASD</span></>}</div>
  {help && <div className="plaza-help-popover"><strong>広場の歩き方</strong><p>矢印キー・WASDで移動します。「入る」または左の部屋一覧から部屋へ移れます。カメラ・マイクは自分でオンにしてください。</p><button onClick={()=>setHelp(false)}>閉じる</button></div>}
 </div>;
}
