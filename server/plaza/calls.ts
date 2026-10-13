import {AccessToken,RoomServiceClient,TrackSource} from 'livekit-server-sdk';
import type {CallCredentials,Participant,RoomId} from '../../shared/plaza/types';
import {ROOMS} from '../../shared/plaza/world';
export interface CallGateway {
 configured:boolean;
 initialize():Promise<void>;
 issue(participant:Participant & {visit:number},generation:number):Promise<CallCredentials>;
 revoke(roomId:RoomId,identity:string):Promise<void>;
}
export class UnconfiguredCalls implements CallGateway {
 configured=false;
 async initialize(){}
 async issue():Promise<CallCredentials>{throw Error('CALL_UNCONFIGURED');}
 async revoke(){}
}
export class LiveKitCalls implements CallGateway {
 configured=true;
 private api:RoomServiceClient;
 constructor(private url:string,private key:string,private secret:string,private prefix:string) {
  const parsed=new URL(url);
  if(parsed.protocol!=='wss:' || !parsed.hostname.endsWith('.livekit.cloud')) throw Error('LIVEKIT_URL must be a LiveKit Cloud wss URL');
  this.api=new RoomServiceClient(url.replace('wss:','https:'),key,secret,{requestTimeout:20});
 }
 private room(id:RoomId){return `${this.prefix}-${id}`;}
 async initialize(){
  const names=ROOMS.filter(r=>r.id!=='lobby').map(r=>this.room(r.id));
  // Dedicated room namespace only. Revoke residual participants on process restart.
  const existing=await this.api.listRooms(names);
  for(const room of existing) for(const participant of await this.api.listParticipants(room.name)) {
   await this.api.removeParticipant(room.name,participant.identity,{revokeTokenTs:BigInt(Math.floor(Date.now()/1000)+1)});
  }
  for(const name of names) await this.api.createRoom({name,maxParticipants:8,emptyTimeout:300});
 }
 async issue(p:Participant & {visit:number},generation:number):Promise<CallCredentials>{
  if(p.roomId==='lobby')throw Error('NO_CALL_IN_LOBBY');
  const identity=`${p.id}:${generation}:${p.visit}`;
  const token=new AccessToken(this.key,this.secret,{identity,name:p.name,ttl:60});
  token.addGrant({roomJoin:true,room:this.room(p.roomId),canPublish:true,canSubscribe:true,canPublishData:false,canUpdateOwnMetadata:false,canPublishSources:[TrackSource.CAMERA,TrackSource.MICROPHONE]});
  return {url:this.url,token:await token.toJwt(),identity,roomId:p.roomId,generation};
 }
 async revoke(roomId:RoomId,identity:string){
  if(roomId==='lobby')return;
  // Strict cutoff avoids Cloud's default one-minute tolerance; identities are never reused.
  await this.api.removeParticipant(this.room(roomId),identity,{revokeTokenTs:BigInt(Math.floor(Date.now()/1000)+1)});
 }
}
export function callsFromEnvironment():CallGateway {
 const {LIVEKIT_URL:url,LIVEKIT_API_KEY:key,LIVEKIT_API_SECRET:secret,LIVEKIT_ROOM_PREFIX:prefix='milife-plaza'}=process.env;
 if(!url && !key && !secret)return new UnconfiguredCalls();
 if(!url || !key || !secret)throw Error('LiveKit settings are incomplete');
 if(!/^[a-zA-Z0-9-]{1,60}$/.test(prefix))throw Error('Invalid LIVEKIT_ROOM_PREFIX');
 return new LiveKitCalls(url,key,secret,prefix);
}
