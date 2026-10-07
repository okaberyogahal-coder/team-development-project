import type {CallCredentials,RoomId} from '../../shared/plaza/types';
import type {CallGateway} from './calls';
import {PlazaState,PlazaError,Session} from './state';
export type RoomChangeResult={session:Session;credentials:CallCredentials|null;error?:string};
export class RoomTransitions {
 constructor(private state:PlazaState,readonly calls:CallGateway){}
 async stopCall(p:Session){
  if(p.callIdentity){const identity=p.callIdentity;await this.calls.revoke(p.roomId,identity);if(p.callIdentity===identity)p.callIdentity=null;}
 }
 async changeRoom(id:string,target:RoomId,requestId:string):Promise<RoomChangeResult>{
  const p=this.state.get(id);this.state.reserveRoom(id,target,requestId);p.busy=true;const generation=p.generation;
  try{
   await this.stopCall(p);
   if(!p.connected || p.generation!==generation)throw new PlazaError('EXPIRED');
   this.state.commitRoom(requestId);
   return await this.issueFor(p,generation);
  }finally{this.state.cancelReservation(requestId);if(p.generation===generation)p.busy=false;}
 }
 async retryCall(id:string):Promise<RoomChangeResult>{
  const p=this.state.get(id);if(p.busy)throw new PlazaError('BUSY');p.busy=true;const g=p.generation;
  try{await this.stopCall(p);if(!p.connected || p.generation!==g)throw new PlazaError('EXPIRED');return await this.issueFor(p,g);}finally{if(p.generation===g)p.busy=false;}
 }
 private current(p:Session,generation:number){return this.state.sessions.get(p.id)===p && p.connected && p.generation===generation;}
 private async issueFor(p:Session,generation:number):Promise<RoomChangeResult>{
  if(!this.current(p,generation))throw new PlazaError('EXPIRED');
  if(p.roomId==='lobby')return {session:p,credentials:null};
  if(!this.calls.configured)return {session:p,credentials:null,error:'CALL_UNCONFIGURED'};
  const snapshot={...this.state.public(p),visit:++p.visit};
  let creds:CallCredentials;
  try{creds=await this.calls.issue(snapshot,generation);}
  catch{
   if(!this.current(p,generation))throw new PlazaError('EXPIRED');
   this.state.toLobby(p.id);return {session:p,credentials:null,error:'CALL_FAILED'};
  }
  if(!this.current(p,generation) || p.roomId!==snapshot.roomId){
   // Never use the mutable Session's new room/identity to revoke an old result.
   await this.calls.revoke(creds.roomId,creds.identity);
   throw new PlazaError('EXPIRED');
  }
  p.callIdentity=creds.identity;
  return {session:p,credentials:creds};
 }
}
