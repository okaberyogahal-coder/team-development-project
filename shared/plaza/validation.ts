import {AVATARS,ROOMS} from './world';
import type {ClientMessage} from './types';
export function parseClientMessage(raw:string):ClientMessage|null {
 if(new TextEncoder().encode(raw).length>4096) return null;
 try {
  const m = JSON.parse(raw);
  if(!m || typeof m!=='object' || Array.isArray(m)) return null;
  if(m.type==='join') {
   if(typeof m.name!=='string' || !m.name.trim() || [...m.name.trim()].length>20 || !AVATARS.some(a=>a.id===m.avatar)) return null;
   if(m.resumeToken!==undefined && (typeof m.resumeToken!=='string' || m.resumeToken.length>128)) return null;
   return {type:'join',name:m.name.trim(),avatar:m.avatar,...(m.resumeToken?{resumeToken:m.resumeToken}:{})};
  }
  if(m.type==='move' && [m.x,m.y].every(v=>typeof v==='number' && Number.isFinite(v) && Math.abs(v)<=1) && Number.isSafeInteger(m.seq) && m.seq>=0) return {type:'move',x:m.x,y:m.y,seq:m.seq};
  if(m.type==='changeRoom' && ROOMS.some(r=>r.id===m.roomId) && typeof m.requestId==='string' && /^[a-zA-Z0-9-]{1,64}$/.test(m.requestId)) return {type:'changeRoom',roomId:m.roomId,requestId:m.requestId};
  if(m.type==='leave' || m.type==='retryCall') return {type:m.type};
  return null;
 }catch{return null;}
}
