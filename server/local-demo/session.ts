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
