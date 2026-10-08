import {Group,Vector3} from 'three';
import type {RoomId} from '../../../../shared/plaza/types';
import {roomById} from '../../../../shared/plaza/world';
import {Assets,shape} from './assets';
import {toWorld} from './coordinates';

export function createScenery(roomId:RoomId,assets:Assets){
 const group=new Group();const doors:Array<{roomId:RoomId;position:Vector3}>=[];
 const add=(kind:Parameters<typeof shape>[1],color:string,p:number[],s:number[],parent=group)=>{const m=shape(assets,kind,color,p,s);parent.add(m);return m;};
 const tree=(x:number,z:number,size=1)=>{
  const g=new Group();g.position.set(x,0,z);g.scale.setScalar(size);group.add(g);
  add('cylinder','#b38d6c',[0,0.55,0],[0.14,1.1,0.14],g);
  add('sphere','#83b99a',[0,1.55,0],[0.75,0.9,0.67],g);
  add('sphere','#a9cf9d',[-0.3,1.7,0.22],[0.48,0.63,0.48],g);
  add('sphere','#91c4a0',[0.3,1.5,0.2],[0.5,0.6,0.48],g);
 };
 const plant=(x:number,z:number)=>{add('cylinder','#d5a67e',[x,0.17,z],[0.2,0.34,0.2]);add('sphere','#87b996',[x,0.45,z],[0.3,0.32,0.3]);};
 const bench=(x:number,z:number)=>{
  add('box','#c99a6b',[x,0.42,z],[1.15,0.12,0.4]);add('box','#d9b181',[x,0.74,z-0.19],[1.15,0.35,0.08]);
  for(const dx of [-0.43,0.43])add('box','#a98768',[x+dx,0.2,z],[0.09,0.4,0.3]);
 };
 if(roomId==='lobby'){
  add('box','#b9d39e',[0,-0.22,0],[20.8,0.44,14.8]);
  add('box','#eadfc0',[0,0.008,0],[2.6,0.04,14]);add('box','#eadfc0',[0,0.01,0],[20,0.04,1.8]);
  add('cylinder','#ebdfc0',[0,0.03,0],[3.25,0.08,2.35]);
  add('cylinder','#d4c5a9',[0,0.16,-0.6],[1.45,0.28,1.15]);
  add('cylinder','#f5ecd6',[0,0.3,-0.6],[1.34,0.2,1.04]);
  add('cylinder','#9bd0d4',[0,0.42,-0.6],[1.17,0.06,0.89]);
  add('cylinder','#f6efdb',[0,0.63,-0.6],[0.18,0.46,0.18]);
  add('sphere','#bce7e0',[0,0.9,-0.6],[0.23,0.27,0.23]);
  for(const [x,z,s] of [[-8.7,-5.1,1.1],[8.7,-5.1,1],[-4,-4.5,1],[-2.6,-4.4,0.85],[2.6,-4.4,0.85],[4,-4.5,1],[-8.9,5.4,0.9],[8.9,5.4,1],[-3.7,5.4,0.85],[3.7,5.4,0.85]])tree(x,z,s);
  bench(-2.35,1.9);bench(2.35,1.9);bench(0,5.5);
  for(const [x,z] of [[-1.9,-2.3],[1.9,-2.3],[-2,2.7],[2,2.7]])plant(x,z);
  // Flowers and stepping stones create detail without external textures.
  for(let i=0;i<32;i++){
   const x=(i%8-3.5)*0.42,z=i<16?-5.65:5.95;
   add('sphere',i%3===0?'#f0bdba':i%3===1?'#fff1c6':'#f9f4dc',[x+(i<16?-6.2:6.2),0.12,z+Math.floor(i/8)%2*0.28],[0.1,0.09,0.1]);
  }
  for(const h of [{id:'cafe' as const,x:90,y:195},{id:'hobby' as const,x:700,y:195},{id:'learning' as const,x:90,y:460},{id:'sharing' as const,x:700,y:460}]){
   const room=roomById(h.id);const p=toWorld({x:h.x+102,y:h.y+40});const g=new Group();g.position.set(p.x,0,p.z);group.add(g);
   add('box','#fff1d8',[0,0.8,0],[3.4,1.6,1.8],g);
   const roof=add('cone',room.color,[0,1.85,0],[2.65,1.15,1.65],g);roof.rotation.y=Math.PI/4;
   add('box','#d8bd8d',[0,0.65,0.92],[0.62,1.3,0.12],g);
   add('sphere','#fff3cb',[0.19,0.65,1.01],[0.035,0.035,0.035],g);
   for(const x of [-1.03,1.03]){
    add('box','#f5d9aa',[x,0.95,0.93],[0.83,0.8,0.1],g);
    add('box','#b1d9d9',[x,0.95,1],[0.65,0.62,0.05],g);
    add('box','#fff9e8',[x,0.95,1.04],[0.05,0.63,0.035],g);
    add('box','#fff9e8',[x,0.95,1.04],[0.65,0.05,0.035],g);
   }
   add('box',room.color,[0,0.055,1.38],[1.1,0.1,0.65],g);
   doors.push({roomId:h.id,position:new Vector3(p.x,0.2,p.z+1.85)});
  }
 }else{
  const room=roomById(roomId);
  add('box','#eee0c6',[0,-0.15,0],[20,0.3,14]);
  for(let i=-9;i<=9;i++)add('box','#dcc9ab',[i,0.01,0],[0.025,0.015,14]);
  add('box','#fff2dc',[0,1.2,-6.6],[20,2.4,0.3]);
  add('box',room.color,[0,0.3,-6.38],[20,0.6,0.07]);
  for(const x of [-7,-2.4,2.4,7]){
   add('box','#d5b88b',[x,1.55,-6.4],[2.2,1.4,0.12]);
   add('box','#b9dfe0',[x,1.55,-6.31],[1.98,1.2,0.07]);
   add('box','#fff8e5',[x,1.55,-6.25],[0.06,1.2,0.04]);
  }
  add('cylinder',room.color,[0,0.025,0],[4.1,0.04,2.8]);
  add('cylinder','#b99670',[0,0.55,-0.6],[0.35,1.1,0.35]);
  add('cylinder','#e9c798',[0,1.12,-0.6],[1.85,0.16,1.3]);plant(0,-0.6);
  for(const [x,z] of [[-2.4,-1.3],[-2.4,0.7],[2.4,-1.3],[2.4,0.7]]){
   add('box',room.color,[x,0.46,z],[0.65,0.18,0.7]);add('box',room.color,[x,0.8,z-0.28],[0.65,0.6,0.12]);
   for(const dx of [-0.23,0.23])add('box','#b5936d',[x+dx,0.2,z],[0.08,0.4,0.5]);
  }
  for(const [x,z] of [[-8,-4.8],[8,-4.8],[-8,5],[8,5]]){plant(x,z);tree(x,z,0.75);}
  bench(-7,0);bench(7,0);
  add('box','#d3b78d',[0,0.045,6],[3.2,0.08,0.8]);
  doors.push({roomId:'lobby',position:new Vector3(0,0.2,6)});
 }
 return {group,doors};
}
