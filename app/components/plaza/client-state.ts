import type {CallCredentials,Participant,RoomSummary,ServerMessage} from '../../../shared/plaza/types';
export type ClientState={status:'idle'|'connecting'|'joined'|'reconnecting'|'full';self:Participant|null;participants:Participant[];rooms:RoomSummary[];total:number;generation:number;credentials:CallCredentials|null;error:string|null;callConfigured:boolean;changing:boolean};
export const initialClientState:ClientState={status:'idle',self:null,participants:[],rooms:[],total:0,generation:0,credentials:null,error:null,callConfigured:false,changing:false};
export const canMove=(s:ClientState)=>s.status==='joined' && !s.changing;
export function receiveMessage(s:ClientState,m:ServerMessage):ClientState{
 if(m.type==='welcome')return {...s,status:'joined',self:m.self,participants:m.participants,rooms:m.rooms,total:m.total,generation:m.generation,callConfigured:m.callConfigured,credentials:null,error:null,changing:false};
 if(m.type==='state'){
  if(m.generation!==s.generation || s.status!=='joined')return s;
  const self=m.participants.find(p=>p.id===s.self?.id) || s.self;
  return {...s,self,participants:m.participants,rooms:m.rooms,total:m.total};
 }
 if(m.type==='roomChanged'){
  if(m.generation!==s.generation)return s;
  return {...s,self:m.self,participants:[m.self],credentials:null,changing:false,error:null};
 }
 if(m.type==='callCredentials'){
  if(m.credentials.generation!==s.generation || m.credentials.roomId!==s.self?.roomId || s.status!=='joined')return s;
  return {...s,credentials:m.credentials};
 }
 if(m.type==='error')return {...s,status:m.code==='FULL'?'full':s.status,error:m.message,changing:false};
 return s;
}
