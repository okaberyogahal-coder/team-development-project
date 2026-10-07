import test from 'node:test';
import assert from 'node:assert/strict';
import {WebSocket} from 'ws';
import {startPlazaServer} from '../../server/plaza/service';
import {UnconfiguredCalls} from '../../server/plaza/calls';
import type {ServerMessage} from '../../shared/plaza/types';
const open=(url:string,origin='http://localhost:3002')=>new Promise<WebSocket>((resolve,reject)=>{const s=new WebSocket(url,{origin});s.once('open',()=>resolve(s));s.once('error',reject);});
function next(s:WebSocket,type:string):Promise<ServerMessage>{return new Promise((resolve,reject)=>{const timer=setTimeout(()=>{s.off('message',listener);reject(Error(`timeout ${type}`));},2500);const listener=(data:Buffer)=>{const m=JSON.parse(data.toString());if(m.type===type){clearTimeout(timer);s.off('message',listener);resolve(m);}};s.on('message',listener);});}
test('実際のWebSocketで移動と部屋変更を共有する',async()=>{
 const server=await startPlazaServer({port:0,origins:['http://localhost:3002'],calls:new UnconfiguredCalls()});
 const a=await open(server.url),b=await open(server.url);
 try{
  const welcomeA=next(a,'welcome');a.send(JSON.stringify({type:'join',name:'花子',avatar:'mint'}));const wa=await welcomeA;if(wa.type!=='welcome')throw Error();
  const welcomeB=next(b,'welcome');b.send(JSON.stringify({type:'join',name:'太郎',avatar:'sky'}));await welcomeB;
  const state=next(b,'state');a.send(JSON.stringify({type:'move',x:1,y:0,seq:1}));let snapshot=await state;
  while(snapshot.type==='state' && snapshot.participants.find(p=>p.id===wa.self.id)!.x===wa.self.x)snapshot=await next(b,'state');
  assert.equal(snapshot.type,'state');if(snapshot.type==='state')assert.ok(snapshot.participants.find(p=>p.id===wa.self.id)!.x>wa.self.x);
  const changed=next(a,'roomChanged');a.send(JSON.stringify({type:'changeRoom',roomId:'cafe',requestId:'a'}));assert.equal((await changed).type,'roomChanged');
  const newState=await next(b,'state');if(newState.type==='state')assert.equal(newState.participants.length,1);
  const error=next(a,'error');a.send(JSON.stringify({type:'move',x:999,y:0,seq:2}));assert.equal((await error).type,'error');
 }finally{a.terminate();b.terminate();await server.close();}
});
test('許可されていないOriginを拒否する',async()=>{
 const server=await startPlazaServer({port:0,origins:['http://localhost:3002'],calls:new UnconfiguredCalls()});
 try{await assert.rejects(open(server.url,'https://evil.example'),/403/);}finally{await server.close();}
});
test('通話cleanup失敗をwelcomeの後に通知し、復帰の案内を残す',async()=>{
 const g={configured:true,initialize:async()=>{},issue:async(p:any,generation:number)=>({url:'x',token:'x',identity:`${p.id}:${generation}`,roomId:p.roomId,generation}),revoke:async()=>{throw Error('offline');}};
 const server=await startPlazaServer({port:0,origins:['http://localhost:3002'],calls:g});const a=await open(server.url);let b:WebSocket|null=null;
 try{
  const welcome=next(a,'welcome');a.send(JSON.stringify({type:'join',name:'a',avatar:'sky'}));const w=await welcome;if(w.type!=='welcome')throw Error();
  const credentials=next(a,'callCredentials');a.send(JSON.stringify({type:'changeRoom',roomId:'cafe',requestId:'a'}));await credentials;
  b=await open(server.url);const sequence:string[]=[];b.on('message',data=>{const m=JSON.parse(data.toString());if(m.type==='welcome' || m.code==='CALL_RECOVERY')sequence.push(m.type);});
  const resumed=next(b,'welcome');b.send(JSON.stringify({type:'join',name:'a',avatar:'sky',resumeToken:w.resumeToken}));await resumed;
  await next(b,'state');assert.deepEqual(sequence,['welcome','error']);
 }finally{a.terminate();b?.terminate();await server.close();}
});
