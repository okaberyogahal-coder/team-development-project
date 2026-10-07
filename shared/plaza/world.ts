import type {AvatarId,RoomId} from './types';
export const AVATARS: {id:AvatarId;label:string;color:string}[] = [
 {id:'coral',label:'コーラル',color:'#f5828c'}, {id:'sky',label:'そら',color:'#65a9ec'},
 {id:'mint',label:'ミント',color:'#53baa0'}, {id:'violet',label:'すみれ',color:'#9c8cda'}, {id:'sun',label:'ひなた',color:'#eeb755'},
];
export const ROOMS: {id:RoomId;name:string;description:string;color:string;icon:string;capacity:number}[] = [
 {id:'lobby',name:'ミライフ広場',description:'出会いがはじまる、みんなの広場',color:'#70b89b',icon:'🌳',capacity:50},
 {id:'cafe',name:'交流カフェ',description:'お茶を飲むように、気軽なおしゃべり',color:'#e99a81',icon:'☕',capacity:8},
 {id:'hobby',name:'趣味の部屋',description:'好きなことを、誰かと一緒に',color:'#b095cf',icon:'🎨',capacity:8},
 {id:'learning',name:'学びの部屋',description:'わからないことから、新しい発見へ',color:'#79abd3',icon:'📘',capacity:8},
 {id:'sharing',name:'経験共有の部屋',description:'あなたの経験が、誰かのヒントに',color:'#d6b16b',icon:'🌱',capacity:8},
];
export const WORLD = {width:1000,height:700,padding:26,speed:120};
export const SPAWN = {x:500,y:540};
export const WALLS = [{x:260,y:75,width:150,height:95},{x:590,y:75,width:150,height:95}];
export const roomById = (id:RoomId) => ROOMS.find(r=>r.id===id)!;
