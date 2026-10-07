'use client';
import {useEffect,useRef,useState} from 'react';
import {Room,RoomEvent,Participant as LiveParticipant,ConnectionState} from 'livekit-client';
import type {CallCredentials} from '../../shared/plaza/types';
import {CallSession,CallSnapshot} from '../components/plaza/call-state';
export function useRoomCall(credentials:CallCredentials|null){
 const [snapshot,setSnapshot]=useState<CallSnapshot>({status:'idle',roomId:null,micEnabled:false,cameraEnabled:false,error:null,busy:false});
 const manager=useRef<CallSession|null>(null);if(!manager.current)manager.current=new CallSession(setSnapshot);
 const [room,setRoom]=useState<Room|null>(null);const [participants,setParticipants]=useState<LiveParticipant[]>([]);const [audioBlocked,setAudioBlocked]=useState(false);
 useEffect(()=>{
  const session=manager.current!;let active=true;
  if(!credentials){void session.disconnect();setRoom(null);setParticipants([]);setAudioBlocked(false);return;}
  const r=new Room({adaptiveStream:true,dynacast:true,videoCaptureDefaults:{resolution:{width:640,height:360,frameRate:20}},publishDefaults:{simulcast:true}});
  setRoom(r);
  const refresh=()=>{if(active){setParticipants([r.localParticipant,...Array.from(r.remoteParticipants.values())]);session.setDevices(r.localParticipant.isMicrophoneEnabled,r.localParticipant.isCameraEnabled);}};
  const disconnected=()=>{if(active){session.lost();setParticipants([]);}};
  const playback=()=>{if(active)setAudioBlocked(!r.canPlaybackAudio);};
  const eventNames=[RoomEvent.ParticipantConnected,RoomEvent.ParticipantDisconnected,RoomEvent.TrackSubscribed,RoomEvent.TrackUnsubscribed,RoomEvent.TrackMuted,RoomEvent.TrackUnmuted,RoomEvent.LocalTrackPublished,RoomEvent.LocalTrackUnpublished];
  eventNames.forEach(e=>r.on(e,refresh));r.on(RoomEvent.Disconnected,disconnected);r.on(RoomEvent.AudioPlaybackStatusChanged,playback);
  void session.connect(credentials,{
   connect:async c=>{await r.connect(c.url,c.token);if(active)refresh();},
   close:async()=>{r.localParticipant.trackPublications.forEach(p=>p.track?.stop());await r.disconnect(true);},
   setMic:async enabled=>{await r.localParticipant.setMicrophoneEnabled(enabled);},
   setCamera:async enabled=>{await r.localParticipant.setCameraEnabled(enabled);},
  });
  return ()=>{active=false;eventNames.forEach(e=>r.off(e,refresh));r.off(RoomEvent.Disconnected,disconnected);r.off(RoomEvent.AudioPlaybackStatusChanged,playback);void session.disconnect();};
 },[credentials]);
 useEffect(()=>{const stop=()=>{void manager.current?.disconnect();};window.addEventListener('pagehide',stop);return()=>window.removeEventListener('pagehide',stop);},[]);
 return {...snapshot,room,participants,audioBlocked,hasCredentials:!!credentials,toggleMic:()=>manager.current?.toggleMic(),toggleCamera:()=>manager.current?.toggleCamera(),disconnect:()=>manager.current?.disconnect(),startAudio:async()=>{try{await room?.startAudio();setAudioBlocked(false);}catch{setAudioBlocked(true);}},connected:room?.state===ConnectionState.Connected};
}
