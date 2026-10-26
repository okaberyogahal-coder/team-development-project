import {randomBytes} from 'node:crypto';
import {isLocalUserId,type LocalUserId} from '../../shared/local-demo';
export const LOCAL_USER_COOKIE='melife.local-demo.user.v1';
/** Fictional user selection only: intentionally NOT authentication or an authorization credential. */
export function getSelectedLocalUser(request:Request):LocalUserId|null{
  const matches=(request.headers.get('cookie')??'').split(';').map(v=>v.trim()).filter(v=>v.startsWith(`${LOCAL_USER_COOKIE}=`));
  if(matches.length!==1)return null;
  try{const id=decodeURIComponent(matches[0].slice(LOCAL_USER_COOKIE.length+1));return isLocalUserId(id)?id:null;}catch{return null;}
}
export function selectionCookie(id:LocalUserId|null,request:Request):string{
  return `${LOCAL_USER_COOKIE}=${id??''}; Path=/; HttpOnly; SameSite=Lax; Max-Age=${id?604800:0}${new URL(request.url).protocol==='https:'?'; Secure':''}`;
}

export const LOCAL_SESSION_COOKIE='melife.local-demo.session.v2';
const SESSION_TTL=8*60*60*1000;
export class LocalSessionStore{
 private readonly sessions=new Map<string,{userId:LocalUserId;expires:number}>();
 issue(userId:LocalUserId,now:number):string{
  for(const [key,value] of this.sessions)if(value.expires<=now)this.sessions.delete(key);
  while(this.sessions.size>=1000)this.sessions.delete(this.sessions.keys().next().value!);
  const token=randomBytes(32).toString('hex');this.sessions.set(token,{userId,expires:now+SESSION_TTL});return token;
 }
 resolve(token:string|null,now:number):LocalUserId|null{
  if(!token)return null;const session=this.sessions.get(token);
  if(!session)return null;if(session.expires<=now){this.sessions.delete(token);return null;}return session.userId;
 }
 revoke(token:string|null):void{if(token)this.sessions.delete(token);}
}
const processState=globalThis as typeof globalThis&{__melifeLocalSessions?:LocalSessionStore};
export function getLocalSessionStore():LocalSessionStore{return processState.__melifeLocalSessions??=new LocalSessionStore();}
export function getSessionToken(request:Request):string|null{
 const matches=(request.headers.get('cookie')??'').split(';').map(value=>value.trim()).filter(value=>value.startsWith(`${LOCAL_SESSION_COOKIE}=`));
 if(matches.length!==1)return null;
 const token=matches[0].slice(LOCAL_SESSION_COOKIE.length+1);return /^[a-f0-9]{64}$/.test(token)?token:null;
}
export function sessionCookie(token:string|null,request:Request):string{
 return `${LOCAL_SESSION_COOKIE}=${token??''}; Path=/; HttpOnly; SameSite=Lax; Max-Age=${token?SESSION_TTL/1000:0}${new URL(request.url).protocol==='https:'?'; Secure':''}`;
}
