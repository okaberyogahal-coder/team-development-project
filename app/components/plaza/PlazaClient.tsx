'use client';
import {useState} from 'react';
import {Logo} from '../Logo';
import {usePlazaConnection} from '../../hooks/usePlazaConnection';
import {useRoomCall} from '../../hooks/useRoomCall';
import {JoinForm} from './JoinForm';
import {WorldMap} from './WorldMap';
import {RoomPicker} from './RoomPicker';
import {CallPanel} from './CallPanel';
import {ConnectionStatus} from './ConnectionStatus';
import {Avatar} from './Avatar';
import {roomById} from '../../../shared/plaza/world';
export function PlazaClient(){
 const plaza=usePlazaConnection();const call=useRoomCall(plaza.credentials);const [showPeople,setShowPeople]=useState(false);
 const entered=!!plaza.self;const room=roomById(plaza.self?.roomId || 'lobby');
 const exit=()=>{void call.disconnect();plaza.leave();};
 return <main className="plaza-shell">
  <header className="plaza-header"><Logo href="/"/><div className="plaza-header-title">みんなの交流空間 <span>PC TEST</span></div><ConnectionStatus status={plaza.status} total={plaza.total}/>{entered?<button className="plaza-exit" onClick={exit}>退出する ↗</button>:<a className="plaza-back" href="/">トップページへ ↗</a>}</header>
  {!entered?<div className="plaza-entry"><JoinForm onJoin={plaza.join} status={plaza.status} error={plaza.error} onCancel={exit}/><footer className="plaza-entry-footer">何歳からでも、新しい未来へ。 <span>MIRAI LIFE</span></footer></div>:<>
   <div className="plaza-workspace">
    <aside className="plaza-sidebar"><div className="plaza-sidebar-heading"><span className="plaza-eyebrow">YOUR COMMUNITY</span><h1>ミライフ広場</h1><p>今日はどんな出会いがあるかな。</p></div><RoomPicker roomId={room.id} rooms={plaza.rooms} onChange={plaza.changeRoom} disabled={plaza.status!=='joined' || plaza.changing}/><div className="plaza-sidebar-bottom"><span>🌱</span><strong>「こんにちは」からはじめよう</strong><p>教える人も、教わる人も。<br/>ここではみんなが主役です。</p></div></aside>
    <section className="plaza-center" aria-label="交流空間"><div className="plaza-space-heading"><div><span className="plaza-eyebrow">{room.id==='lobby'?'THE PLAZA':'COMMUNITY ROOM'}</span><h2>{room.name}</h2><p>{room.description}</p></div><button className="plaza-people-button" onClick={()=>setShowPeople(!showPeople)}>♧ {plaza.participants.length}人 <span>参加者</span></button></div>
     {plaza.error && <div className="plaza-notice" role="alert"><span>{plaza.error}</span><button onClick={plaza.dismissError} aria-label="案内を閉じる">×</button></div>}
     <WorldMap roomId={room.id} participants={plaza.participants} selfId={plaza.self!.id} onMove={plaza.move} onRoom={plaza.changeRoom} disabled={plaza.status!=='joined' || plaza.changing}/>
     <div className="plaza-bottom-bar"><div className="plaza-self-summary"><Avatar avatar={plaza.self!.avatar} size={32}/><div><strong>{plaza.self!.name}</strong><span>あなたのアバター</span></div></div><span className="plaza-map-caption">{plaza.changing?'部屋を移動しています…':room.id==='lobby'?'気になる部屋を選んで、会話をはじめよう。':'同じ部屋の人と、ゆっくりおしゃべり。'}</span><span className="plaza-small-tag">{room.id==='lobby'?'FREE SPACE':'ROOM '+(plaza.rooms.findIndex(r=>r.id===room.id))}</span></div>
     {showPeople && <div className="plaza-participant-list"><h3>この場所の参加者</h3>{plaza.participants.map(p=><div key={p.id}><Avatar avatar={p.avatar} size={24}/><span>{p.name}</span><small>{p.connected?'オンライン':'再接続待ち'}</small></div>)}</div>}
    </section>
    <aside className="plaza-right"><CallPanel call={call} configured={plaza.callConfigured} inLobby={room.id==='lobby'} onRetry={plaza.retryCall}/><div className="plaza-room-guide"><span>小さな会話、大きなきっかけ。</span><p>カメラなしでも大丈夫。<br/>あなたのペースで参加してください。</p></div></aside>
   </div>
   <footer className="plaza-footer"><span className="plaza-dot"/>ここから、次の挑戦へ。<span>PCテスト版 · MIRAI LIFE</span></footer>
  </>}
 </main>;
}
