import {Group,Mesh,MeshStandardMaterial} from 'three';
import type {AvatarId} from '../../../../shared/plaza/types';
import {AVATARS} from '../../../../shared/plaza/world';
import {Assets,shape} from './assets';

export function createAvatar(avatar:AvatarId,self:boolean,assets:Assets){
 const group=new Group();const color=AVATARS.find(a=>a.id===avatar)!.color;
 const add=(kind:Parameters<typeof shape>[1],tint:string,p:number[],s:number[],name?:string)=>{const m=shape(assets,kind,tint,p,s);if(name)m.name=name;group.add(m);return m;};
 const skin='#ffdfbd',hair='#685044';
 const shadow=add('cylinder','#abc09c',[0,0.025,0],[0.36,0.03,0.27]);shadow.castShadow=false;
 add('sphere',color,[0,0.56,0],[0.25,0.3,0.18],'shirt');
 add('sphere',skin,[0,1.04,0],[0.31,0.31,0.28]);
 add('sphere',hair,[0,1.19,-0.045],[0.32,0.19,0.27]);
 add('sphere',hair,[-0.23,1.11,0.13],[0.09,0.15,0.07]);
 add('sphere',hair,[0.23,1.11,0.13],[0.09,0.15,0.07]);
 for(const x of [-0.1,0.1]){
  add('sphere','#3b433f',[x,1.045,0.26],[0.023,0.035,0.015]);
  add('sphere','#f2b8a1',[x*1.6,0.96,0.25],[0.038,0.022,0.012]);
 }
 add('sphere','#bc866d',[0,0.965,0.277],[0.035,0.013,0.01]);
 const leftLeg=new Group(),rightLeg=new Group();
 for(const [leg,x,name] of [[leftLeg,-0.105,'left-leg'],[rightLeg,0.105,'right-leg']] as const){
  leg.name=name;leg.position.set(x,0.33,0);
  leg.add(shape(assets,'cylinder','#637a73',[0,-0.08,0],[0.072,0.23,0.075]));
  leg.add(shape(assets,'sphere','#fff6de',[0,-0.23,0.04],[0.095,0.07,0.14]));group.add(leg);
 }
 const arms:Group[]=[];
 for(const x of [-0.28,0.28]){
  const arm=new Group();arm.position.set(x,0.73,0);
  arm.add(shape(assets,'sphere',color,[0,-0.065,0],[0.085,0.15,0.08]));
  arm.add(shape(assets,'sphere',skin,[0,-0.22,0],[0.072,0.08,0.07]));group.add(arm);arms.push(arm);
 }
 if(self){const ring=add('ring','#fff9d0',[0,0.06,0],[0.43,0.43,0.43]);ring.rotation.x=-Math.PI/2;ring.castShadow=false;}
 const originals=new Map<Mesh,string>();group.traverse(o=>{if(o instanceof Mesh)originals.set(o,(o.material as MeshStandardMaterial).color.getStyle());});
 return {group,
  animate(distance:number,time:number){const stride=distance>0.005?Math.sin(time*11)*0.42:0;leftLeg.rotation.x=stride;rightLeg.rotation.x=-stride;arms[0].rotation.x=-stride*0.7;arms[1].rotation.x=stride*0.7;},
  setConnected(connected:boolean){originals.forEach((tint,m)=>{m.material=assets.material(tint,connected?1:0.45);});},
 };
}
