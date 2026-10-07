'use client';
import {useEffect,useRef} from 'react';
import {Participant,Track,TrackPublication} from 'livekit-client';
import {canRetryCall} from './call-state';
import type {useRoomCall} from '../../hooks/useRoomCall';
function Media({publication,local}:{publication:TrackPublication;local:boolean}){
 const ref=useRef<HTMLDivElement>(null);
 useEffect(()=>{const track=publication.track;if(!track || !ref.current)return;const element=track.attach();element.autoplay=true;if(element instanceof HTMLVideoElement){element.playsInline=true;element.muted=local;}ref.current.appendChild(element);return()=>{track.detach(element);element.remove();};},[publication.track,local]);
 return <div ref={ref} className={publication.kind===Track.Kind.Audio?'plaza-audio-track':'plaza-video-track'}/>;
}
function Tile({person,local}:{person:Participant;local:boolean}){
 const camera=Array.from(person.videoTrackPublications.values()).find(p=>p.source===Track.Source.Camera && p.track && !p.isMuted);
 const audio=Array.from(person.audioTrackPublications.values()).filter(p=>p.track);
 return <div className="plaza-video-tile">{camera?<Media publication={camera} local={local}/>:<div className="plaza-video-placeholder">{(person.name || 'ゲスト').slice(0,1)}</div>}{!local && audio.map(p=><Media publication={p} local={false} key={p.trackSid}/>)}<span>{person.name || 'ゲスト'}{local?'（あなた）':''}<small>{person.isMicrophoneEnabled?'マイクON':'マイクOFF'}</small></span></div>;
}
export function CallPanel({call,configured,inLobby,onRetry}:{call:ReturnType<typeof useRoomCall>;configured:boolean;inLobby:boolean;onRetry:()=>void}){
 return <section className="plaza-call-panel"><div className="plaza-section-caption">CONVERSATION <span>{inLobby?'ロビーでは通話しません':'この部屋でおしゃべり'}</span></div>
  {inLobby?<div className="plaza-call-empty"><span>☕</span><strong>話したい部屋を選んでみよう</strong><p>同じ部屋に入った人同士で<br/>音声・ビデオ通話ができます。</p></div>:!configured?<div className="plaza-call-empty"><span>🎥</span><strong>ビデオ通話は準備中です</strong><p>部屋の移動はお試しいただけます。<br/>通話は接続設定後に利用できます。</p></div>:<>
   {call.participants.length?<div className="plaza-video-grid">{call.participants.map(p=><Tile key={p.identity} person={p} local={p===call.room?.localParticipant}/>)}</div>:<div className="plaza-call-empty"><strong>{call.status==='connecting'?'通話に接続しています…':'通話に参加してみよう'}</strong></div>}
   {call.error && <p className="plaza-error" role="alert">{call.error}</p>}
   {canRetryCall(configured,inLobby,call.status,call.hasCredentials) && <button className="plaza-secondary" onClick={onRetry}>通話に再接続</button>}
   {call.audioBlocked && <button className="plaza-secondary" onClick={call.startAudio}>音声を開始する</button>}
  </>}
  <div className="plaza-call-controls"><button onClick={call.toggleMic} disabled={inLobby || !configured || call.status!=='connected' || call.busy} aria-pressed={call.micEnabled}><span>{call.micEnabled?'🎙':'🔇'}</span>マイク {call.micEnabled?'ON':'OFF'}</button><button onClick={call.toggleCamera} disabled={inLobby || !configured || call.status!=='connected' || call.busy} aria-pressed={call.cameraEnabled}><span>▣</span>カメラ {call.cameraEnabled?'ON':'OFF'}</button></div>
  <p className="plaza-call-note">カメラ・マイクは自分でオンにできます。</p>
 </section>;
}
