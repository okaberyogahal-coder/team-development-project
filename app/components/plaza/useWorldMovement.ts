'use client';
import {useEffect,useRef} from 'react';
import type {Direction,RoomId} from '../../../shared/plaza/types';
export function useWorldMovement(onMove:(d:Direction)=>void,disabled:boolean,roomId:RoomId){
 const keys=useRef(new Set<string>());
 useEffect(()=>{
  const stopped=()=>{keys.current.clear();onMove({x:0,y:0});};
  const update=()=>onMove({x:Number(keys.current.has('arrowright')||keys.current.has('d'))-Number(keys.current.has('arrowleft')||keys.current.has('a')),y:Number(keys.current.has('arrowdown')||keys.current.has('s'))-Number(keys.current.has('arrowup')||keys.current.has('w'))});
  const down=(e:KeyboardEvent)=>{
   const target=e.target;const key=e.key.toLowerCase();if(target instanceof HTMLElement && target.closest('input,textarea,select,[contenteditable="true"]'))return;
   if(!['arrowup','arrowdown','arrowleft','arrowright','w','a','s','d'].includes(key) || disabled)return;
   e.preventDefault();if(keys.current.has(key))return;keys.current.add(key);update();
  };
  const up=(e:KeyboardEvent)=>{if(keys.current.delete(e.key.toLowerCase()))update();};
  window.addEventListener('keydown',down);window.addEventListener('keyup',up);window.addEventListener('blur',stopped);document.addEventListener('visibilitychange',stopped);
  const focus=()=>{if(document.activeElement?.matches('input,textarea,select,[contenteditable="true"]'))stopped();};document.addEventListener('focusin',focus);
  return ()=>{stopped();window.removeEventListener('keydown',down);window.removeEventListener('keyup',up);window.removeEventListener('blur',stopped);document.removeEventListener('visibilitychange',stopped);document.removeEventListener('focusin',focus);};
 },[onMove,disabled,roomId]);
}
