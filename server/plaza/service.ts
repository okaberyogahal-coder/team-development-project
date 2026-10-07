import {createServer} from 'node:http';
import type {AddressInfo} from 'node:net';
import {WebSocket,WebSocketServer} from 'ws';
import {parseClientMessage} from '../../shared/plaza/validation';
import type {ServerMessage} from '../../shared/plaza/types';
import {PlazaError,PlazaState,Session} from './state';
import type {CallGateway} from './calls';
import {RoomTransitions,RoomChangeResult} from './transitions';
const messages:Record<string,string>={FULL:'広場が満員です。時間をおいてもう一度お試しください。',ROOM_FULL:'この部屋は満員です。別の部屋を選んでください。',BUSY:'部屋を移動中です。少しお待ちください。',EXPIRED:'移動を完了できませんでした。もう一度お試しください。',CALL_UNCONFIGURED:'ビデオ通話は準備中です。部屋での移動はお試しいただけます。',CALL_FAILED:'通話につながりませんでした。ロビーからもう一度お試しください。',NOT_JOINED:'先に広場へ入場してください。',INVALID:'入力を確認してください。',CALL_RECOVERY:'前の通話を終了できませんでした。時間をおいて再試行してください。'};
export async function startPlazaServer(options:{port:number;origins:string[];calls:CallGateway}) {
 if(!Number.isInteger(options.port) || options.port<0 || options.port>65535 || !options.origins.length)throw Error('Invalid plaza server settings');
 await options.calls.initialize();
 const state=new PlazaState();const transitions=new RoomTransitions(state,options.calls);
 const server=createServer((req,res)=>{if(req.url!=='/health'){res.writeHead(404);res.end();return;}res.setHeader('Content-Type','application/json');res.end(JSON.stringify({ok:true,participants:state.total,callConfigured:options.calls.configured}));});
 const wss=new WebSocketServer({noServer:true,maxPayload:4096});
 const sockets=new Map<string,{socket:WebSocket;generation:number}>();
 const cleanup=new Map<string,{p:Session;promise:Promise<void>|null}>();
 const send=(ws:WebSocket,m:ServerMessage)=>{if(ws.readyState===WebSocket.OPEN && ws.bufferedAmount<256000)ws.send(JSON.stringify(m));};
 const error=(ws:WebSocket,code:string)=>send(ws,{type:'error',code,message:messages[code] || '接続に問題が起きました。もう一度お試しください。'});
 function cleanCall(p:Session):Promise<void>{
  const previous=cleanup.get(p.id);if(previous?.promise)return previous.promise;
  const job=previous || {p,promise:null};cleanup.set(p.id,job);
  const work=transitions.stopCall(p).then(()=>{cleanup.delete(p.id);},()=>{job.promise=null;throw new PlazaError('CALL_RECOVERY');});job.promise=work;return work;
 }
 server.on('upgrade',(req,socket,head)=>{
  if(!req.headers.origin || !options.origins.includes(req.headers.origin)){socket.end('HTTP/1.1 403 Forbidden\r\nConnection: close\r\n\r\n');return;}
  // Bound pre-join resources as well as admitted participants.
  if(wss.clients.size>=120){socket.end('HTTP/1.1 503 Service Unavailable\r\nConnection: close\r\n\r\n');return;}
  wss.handleUpgrade(req,socket,head,ws=>wss.emit('connection',ws,req));
 });
 wss.on('connection',ws=>{
  let id:string|null=null,generation=0,joining=false,left=false,lastPong=Date.now(),windowStart=Date.now(),count=0;
  const joinTimer=setTimeout(()=>{if(!id)ws.close(1008,'Join required');},10000);
  ws.on('pong',()=>{lastPong=Date.now();});
  ws.on('error',()=>{});
  const heartbeat=setInterval(()=>{if(Date.now()-lastPong>20000)ws.terminate();else if(ws.readyState===WebSocket.OPEN)ws.ping();},10000);
  function sendResult(result:RoomChangeResult){
   if(!id || sockets.get(id)?.socket!==ws || !result.session.connected || result.session.generation!==generation)return;
   send(ws,{type:'roomChanged',self:state.public(result.session),generation});
   if(result.credentials)send(ws,{type:'callCredentials',credentials:result.credentials});
   if(result.error)error(ws,result.error);
  }
  ws.on('message',async(raw,isBinary)=>{
   if(Date.now()-windowStart>=1000){windowStart=Date.now();count=0;}
   if(++count>30){ws.close(1008,'Rate limit');return;}
   const m=isBinary?null:parseClientMessage(raw.toString());if(!m){error(ws,'INVALID');return;}
   try{
    if(m.type==='join'){
     if(id || joining){error(ws,'BUSY');return;}joining=true;
     const joined=state.join(m);const p=joined.session;id=p.id;generation=p.generation;clearTimeout(joinTimer);
     const old=sockets.get(id);sockets.set(id,{socket:ws,generation});old?.socket.close(1000,'Connection replaced');
     let recoveryFailed=false;
     if(joined.resumed){try{await cleanCall(p);}catch{recoveryFailed=true;}}
     if(ws.readyState!==WebSocket.OPEN || p.generation!==generation){return;}
     send(ws,{type:'welcome',self:state.public(p),resumeToken:p.token,generation,participants:state.participants(p.roomId),rooms:state.summaries(),total:state.total,callConfigured:options.calls.configured});
     if(recoveryFailed)error(ws,'CALL_RECOVERY');
     if(joined.resumed && p.roomId!=='lobby' && !cleanup.has(p.id))sendResult(await transitions.retryCall(p.id));
     return;
    }
    if(!id || sockets.get(id)?.socket!==ws || state.get(id).generation!==generation){error(ws,'NOT_JOINED');return;}
    if(m.type==='move')state.move(id,{x:m.x,y:m.y},m.seq);
    if(m.type==='changeRoom'){if(cleanup.has(id))await cleanCall(state.get(id));sendResult(await transitions.changeRoom(id,m.roomId,m.requestId));}
    if(m.type==='retryCall'){if(cleanup.has(id))await cleanCall(state.get(id));sendResult(await transitions.retryCall(id));}
    if(m.type==='leave'){
     left=true;const p=state.get(id);state.leave(id);sockets.delete(id);void cleanCall(p).catch(()=>{});ws.close(1000,'Left plaza');
    }
   }catch(e){error(ws,e instanceof PlazaError?e.code:'CALL_RECOVERY');}
  });
  ws.on('close',()=>{
   clearTimeout(joinTimer);clearInterval(heartbeat);
   if(!id || left || sockets.get(id)?.socket!==ws)return;
   const p=state.sessions.get(id);sockets.delete(id);state.disconnect(id,generation);
   if(p)void cleanCall(p).catch(()=>{});
  });
 });
 let previous=Date.now(),ticks=0;
 const tick=setInterval(()=>{
  const now=Date.now();state.tick(now,Math.min(now-previous,100));previous=now;
  if(++ticks%2!==0)return;
  for(const [id,{socket,generation}] of sockets){const p=state.sessions.get(id);if(p && p.connected)send(socket,{type:'state',participants:state.participants(p.roomId),rooms:state.summaries(),total:state.total,generation});}
 },50);
 const retryCleanup=setInterval(()=>{for(const job of cleanup.values())if(!job.promise)void cleanCall(job.p).catch(()=>{});},2000);
 await new Promise<void>((resolve,reject)=>{server.once('error',reject);server.listen(options.port,()=>{server.off('error',reject);resolve();});});
 const port=(server.address() as AddressInfo).port;
 return {state,url:`ws://localhost:${port}`,async close(){clearInterval(tick);clearInterval(retryCleanup);for(const ws of wss.clients)ws.terminate();await new Promise<void>(r=>wss.close(()=>r()));await new Promise<void>(r=>server.close(()=>r()));}};
}
