import {randomBytes,randomUUID} from 'node:crypto';
import type {AvatarId,Direction,Participant,RoomId,RoomSummary} from '../../shared/plaza/types';
import {ROOMS,SPAWN} from '../../shared/plaza/world';
import {advance} from '../../shared/plaza/movement';
export class PlazaError extends Error {constructor(public code:string){super(code);}}
export type Session=Participant & {token:string;generation:number;direction:Direction;seq:number;lastInput:number;disconnectedAt:number|null;callIdentity:string|null;busy:boolean;visit:number};
export type JoinInput={name:string;avatar:AvatarId;resumeToken?:string};
type Reservation={id:string;participantId:string;target:RoomId;expires:number;generation:number};
export class PlazaState {
 readonly sessions=new Map<string,Session>();
 private reservations=new Map<string,Reservation>();
 get total(){return this.sessions.size;}
 join(input:JoinInput,now=Date.now()) {
  const existing=input.resumeToken ? Array.from(this.sessions.values()).find(p=>p.token===input.resumeToken):undefined;
  if(existing && (existing.disconnectedAt===null || now-existing.disconnectedAt<30000)) {
   existing.generation++;existing.connected=true;existing.disconnectedAt=null;existing.direction={x:0,y:0};existing.seq=-1;existing.busy=false;
   this.cancelFor(existing.id);return {session:existing,resumed:true};
  }
  if(existing) this.leave(existing.id);
  if(this.total>=50) throw new PlazaError('FULL');
  const p:Session={id:randomUUID(),name:input.name,avatar:input.avatar,roomId:'lobby',...SPAWN,facing:0,connected:true,token:randomBytes(32).toString('hex'),generation:1,direction:{x:0,y:0},seq:-1,lastInput:now,disconnectedAt:null,callIdentity:null,busy:false,visit:0};
  Object.assign(p,this.spawnFor('lobby'));this.sessions.set(p.id,p);return {session:p,resumed:false};
 }
 get(id:string){const p=this.sessions.get(id);if(!p) throw new PlazaError('NOT_JOINED');return p;}
 public(p:Session):Participant {const {id,name,avatar,roomId,x,y,facing,connected}=p;return {id,name,avatar,roomId,x,y,facing,connected};}
 participants(roomId:RoomId){return Array.from(this.sessions.values()).filter(p=>p.roomId===roomId).map(p=>this.public(p));}
 summaries():RoomSummary[]{return ROOMS.map(r=>({id:r.id,capacity:r.capacity,count:r.id==='lobby'?Array.from(this.sessions.values()).filter(p=>p.roomId==='lobby').length:this.occupancy(r.id)}));}
 private spawnFor(roomId:RoomId){
  const people=this.participants(roomId);
  for(let slot=0;slot<50;slot++){const position={x:500+[0,1,-1,2,-2,3,-3,4,-4,-5][slot%10]*40,y:500+Math.floor(slot/10)*40};if(people.every(p=>Math.hypot(p.x-position.x,p.y-position.y)>=35))return position;}
  return {...SPAWN};
 }
 private occupancy(roomId:RoomId){return Array.from(this.sessions.values()).filter(p=>p.roomId===roomId).length+Array.from(this.reservations.values()).filter(r=>r.target===roomId).length;}
 reserveRoom(id:string,target:RoomId,requestId:string,now=Date.now()) {
  const p=this.get(id);if(p.busy || Array.from(this.reservations.values()).some(r=>r.participantId===id)) throw new PlazaError('BUSY');
  if(p.roomId===target) throw new PlazaError('SAME_ROOM');
  if(this.reservations.has(requestId)) throw new PlazaError('BUSY');
  if(target!=='lobby' && this.occupancy(target)>=8) throw new PlazaError('ROOM_FULL');
  const r={id:requestId,participantId:id,target,expires:now+10000,generation:p.generation};this.reservations.set(requestId,r);p.direction={x:0,y:0};return r;
 }
 commitRoom(requestId:string,now=Date.now()) {
  const r=this.reservations.get(requestId);if(!r || r.expires<=now) {this.cancelReservation(requestId);throw new PlazaError('EXPIRED');}
  const p=this.get(r.participantId);if(!p.connected || p.generation!==r.generation) throw new PlazaError('EXPIRED');
  const spawn=this.spawnFor(r.target);p.roomId=r.target;Object.assign(p,spawn);p.direction={x:0,y:0};p.seq=-1;this.reservations.delete(requestId);return p;
 }
 cancelReservation(id:string){this.reservations.delete(id);}
 private cancelFor(id:string){for(const [key,r] of this.reservations) if(r.participantId===id) this.reservations.delete(key);}
 toLobby(id:string){const p=this.get(id);this.cancelFor(id);const spawn=this.spawnFor('lobby');p.roomId='lobby';Object.assign(p,spawn);p.direction={x:0,y:0};return p;}
 leave(id:string){const p=this.sessions.get(id);if(p){p.connected=false;p.direction={x:0,y:0};}this.cancelFor(id);this.sessions.delete(id);}
 disconnect(id:string,generation:number,now=Date.now()) {const p=this.sessions.get(id);if(!p || p.generation!==generation) return;p.connected=false;p.direction={x:0,y:0};p.disconnectedAt=now;this.cancelFor(id);}
 move(id:string,direction:Direction,seq:number,now=Date.now()){const p=this.get(id);if(!p.connected || p.busy || seq<=p.seq) return;p.direction=direction;p.seq=seq;p.lastInput=now;if(direction.x || direction.y)p.facing=Math.atan2(direction.y,direction.x);}
 tick(now:number,dt:number){
  for(const [key,r] of this.reservations) if(r.expires<=now)this.reservations.delete(key);
  for(const p of this.sessions.values()) {
   if(p.disconnectedAt!==null && now-p.disconnectedAt>=30000){this.leave(p.id);continue;}
   if(!p.connected || p.busy || now-p.lastInput>=500) continue;
   Object.assign(p,advance(p,p.direction,dt,p.roomId));
  }
 }
}
