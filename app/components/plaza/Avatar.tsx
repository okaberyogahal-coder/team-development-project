import {AVATARS} from '../../../shared/plaza/world';
import type {AvatarId} from '../../../shared/plaza/types';
export function Avatar({avatar,size=64}:{avatar:AvatarId;size?:number}){
 const color=AVATARS.find(a=>a.id===avatar)?.color || '#65a9ec';
 return <svg viewBox="0 0 64 76" width={size} height={size*76/64} aria-hidden="true">
  <ellipse cx="32" cy="70" rx="19" ry="5" fill="#324b42" opacity=".15"/>
  <path d="M22 53h8v17h-9zm12 0h8l1 17h-9" fill="#425969"/><path d="M20 67h10v5H17q0-5 3-5m14 0h10l3 5H34" fill="#fff"/>
  <path d="M17 41q0-10 15-10t15 10v15H17z" fill={color}/><path d="M18 39l-4 14m32-14 4 14" stroke="#f4cda8" strokeWidth="7" strokeLinecap="round"/>
  <rect x="27" y="27" width="10" height="11" rx="4" fill="#edbe97"/>
  <ellipse cx="32" cy="20" rx="14" ry="16" fill="#f4cda8"/><path d="M18 22V16q0-14 14-14t14 14v6l-4-9q-13 5-21 0z" fill="#46534e"/>
  <circle cx="27" cy="21" r="1.4" fill="#46534e"/><circle cx="37" cy="21" r="1.4" fill="#46534e"/><path d="M28 27q4 3 8 0" fill="none" stroke="#ae7668" strokeWidth="1.5" strokeLinecap="round"/>
 </svg>;
}
