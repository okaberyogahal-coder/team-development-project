import assert from 'node:assert/strict';
import {performance} from 'node:perf_hooks';
import {WebSocket} from 'ws';
import {startPlazaServer} from '../server/plaza/service';
import {UnconfiguredCalls} from '../server/plaza/calls';
import type {ServerMessage,RoomId} from '../shared/plaza/types';
const n=Number(process.argv[process.argv.indexOf('--clients')+1] || 50);
if(![30,50].includes(n))throw Error('--clients must be 30 or 50');
function receive(ws:WebSocket,accept:(m:ServerMessage)=>boolean):Promise<ServerMessage>{return new Promise((resolve,reject)=>{const timer=setTimeout(()=>{ws.off('message',handler);reject(Error('Response timeout'));},5000);const handler=(data:Buffer)=>{const m=JSON.parse(data.toString()) as ServerMessage;if(accept(m)){clearTimeout(timer);ws.off('message',handler);resolve(m);}};ws.on('message',handler);});}
async function main(){
 const server=await startPlazaServer({port:0,origins:['http://localhost:3002'],calls:new UnconfiguredCalls()});
 const sockets:WebSocket[]=[];
 const connect=async()=>{const ws=new WebSocket(server.url,{origin:'http://localhost:3002'});sockets.push(ws);await new Promise<void>((r,j)=>{ws.once('open',()=>r());ws.once('error',j);});return ws;};
 const admit=async()=>{const ws=await connect();const welcome=receive(ws,m=>m.type==='welcome');ws.send(JSON.stringify({type:'join',name:'load',avatar:'sky'}));const m=await welcome;if(m.type!=='welcome')throw Error();return {ws,id:m.self.id,initialX:m.self.x};};
 try{
  const people=await Promise.all(Array.from({length:n},admit));assert.equal(server.state.total,n);
  const started=performance.now();const reflected=receive(people[1].ws,m=>m.type==='state' && m.participants.some(p=>p.id===people[0].id && p.x>people[0].initialX));
  people[0].ws.send(JSON.stringify({type:'move',x:1,y:0,seq:1}));await reflected;const latency=Math.round(performance.now()-started);
  const rooms:RoomId[]=['cafe','hobby','learning','sharing'];
  await Promise.all(people.slice(0,32).map(async(p,i)=>{const changed=receive(p.ws,m=>m.type==='roomChanged');p.ws.send(JSON.stringify({type:'changeRoom',roomId:rooms[Math.floor(i/8)],requestId:`load-${i}`}));const m=await changed;if(m.type==='roomChanged')assert.equal(m.self.roomId,rooms[Math.floor(i/8)]);}));
  assert.equal(server.state.summaries().find(r=>r.id==='cafe')?.count,8);
  const ninth=people[9];const full=receive(ninth.ws,m=>m.type==='error' && m.code==='ROOM_FULL');ninth.ws.send(JSON.stringify({type:'changeRoom',roomId:'cafe',requestId:'ninth'}));await full;
  if(n<50)await Promise.all(Array.from({length:50-n},admit));
  assert.equal(server.state.total,50);const overflow=await connect();const fullPlaza=receive(overflow,m=>m.type==='error' && m.code==='FULL');overflow.send(JSON.stringify({type:'join',name:'overflow',avatar:'mint'}));await fullPlaza;assert.equal(server.state.total,50);
  console.log(JSON.stringify({clients:n,positionReflectionMs:latency,roomCapacity:'8 verified',globalCapacity:'50 verified',videoCalls:'not tested',result:'PASS'}));
 }finally{
  for(const ws of sockets) {if(ws.readyState===WebSocket.OPEN)ws.send(JSON.stringify({type:'leave'}));ws.terminate();}
  await server.close();
 }
}
main().catch(e=>{console.error(e.message);process.exitCode=1;});
