'use client';
import {useCallback,useEffect,useRef,useState} from 'react';
import {canMove,initialClientState,receiveMessage} from '../components/plaza/client-state';
import {createRoomRequestId} from '../../shared/plaza/request-id';
import type {AvatarId,ClientMessage,Direction,RoomId,ServerMessage} from '../../shared/plaza/types';
export function usePlazaConnection(){
 const [state,setState]=useState(initialClientState);const latest=useRef(state);latest.current=state;
 const socket=useRef<WebSocket|null>(null);const active=useRef(false);const timer=useRef<ReturnType<typeof setTimeout>|null>(null);const attempts=useRef(0);const seq=useRef(0);const direction=useRef<Direction>({x:0,y:0});
 const profile=useRef<{name:string;avatar:AvatarId}|null>(null);
 const connectRef=useRef<()=>void>(()=>{});
 const send=useCallback((m:ClientMessage)=>{if(socket.current?.readyState===WebSocket.OPEN)socket.current.send(JSON.stringify(m));},[]);
 const move=useCallback((d:Direction)=>{direction.current=d;if(canMove(latest.current))send({type:'move',...d,seq:++seq.current});},[send]);
 const connect=useCallback(()=>{
  if(!active.current || !profile.current)return;
  let endpoint=process.env.NEXT_PUBLIC_PLAZA_WS_URL;
  if(!endpoint){const u=new URL(window.location.href);u.protocol=u.protocol==='https:'?'wss:':'ws:';u.port='3003';u.pathname='/';u.search='';u.hash='';endpoint=u.toString();}
  let ws:WebSocket;
  try{ws=new WebSocket(endpoint);}catch{setState(s=>({...s,status:'idle',error:'接続先を確認できませんでした。'}));return;}
  socket.current=ws;
  ws.onopen=()=>{
   if(socket.current!==ws || !active.current){ws.close();return;}
   let token:string|undefined;try{token=sessionStorage.getItem('milife-plaza-token') || undefined;}catch{}
   ws.send(JSON.stringify({type:'join',...profile.current,...(token?{resumeToken:token}:{})}));
  };
  ws.onmessage=event=>{
   if(socket.current!==ws || !active.current)return;
   try{
    const m=JSON.parse(event.data) as ServerMessage;
    if(m.type==='welcome'){attempts.current=0;seq.current=0;try{sessionStorage.setItem('milife-plaza-token',m.resumeToken);}catch{}}
    if(m.type==='error' && m.code==='FULL'){active.current=false;ws.close();}
    setState(s=>receiveMessage(s,m));
   }catch{}
  };
  ws.onerror=()=>{};
  ws.onclose=()=>{
   if(socket.current!==ws || !active.current)return;
   direction.current={x:0,y:0};
   setState(s=>({...s,status:'reconnecting',credentials:null,changing:false,error:'広場に接続しています。サーバーの起動を確認して再接続しています。'}));
   const delay=Math.min(8000,1000*2**attempts.current++);timer.current=setTimeout(()=>connectRef.current(),delay);
  };
 },[]);connectRef.current=connect;
 const join=useCallback((name:string,avatar:AvatarId)=>{
  active.current=false;socket.current?.close();if(timer.current)clearTimeout(timer.current);
  profile.current={name,avatar};attempts.current=0;active.current=true;setState({...initialClientState,status:'connecting'});connect();
 },[connect]);
 const leave=useCallback(()=>{
  active.current=false;direction.current={x:0,y:0};if(timer.current)clearTimeout(timer.current);
  send({type:'leave'});socket.current?.close();socket.current=null;
  try{sessionStorage.removeItem('milife-plaza-token');}catch{}setState(initialClientState);
 },[send]);
 const changeRoom=useCallback((roomId:RoomId)=>{
  if(!canMove(latest.current) || latest.current.self?.roomId===roomId)return;
  move({x:0,y:0});setState(s=>({...s,changing:true,error:null}));send({type:'changeRoom',roomId,requestId:createRoomRequestId()});
  // Lost responses must not leave the controls locked forever.
  setTimeout(()=>{if(active.current)setState(s=>s.changing?{...s,changing:false,error:'移動の応答を待っています。もう一度お試しください。'}:s);},11000);
 },[move,send]);
 const retryCall=useCallback(()=>send({type:'retryCall'}),[send]);
 useEffect(()=>{
  const tick=setInterval(()=>{if(canMove(latest.current) && (direction.current.x || direction.current.y))send({type:'move',...direction.current,seq:++seq.current});},100);
  const stop=()=>move({x:0,y:0});window.addEventListener('blur',stop);document.addEventListener('visibilitychange',stop);
  return ()=>{active.current=false;clearInterval(tick);if(timer.current)clearTimeout(timer.current);socket.current?.close();window.removeEventListener('blur',stop);document.removeEventListener('visibilitychange',stop);};
 },[send,move]);
 return {...state,join,leave,move,changeRoom,retryCall,dismissError:()=>setState(s=>({...s,error:null}))};
}
