import {ACESFilmicToneMapping,Color,DirectionalLight,Group,HemisphereLight,OrthographicCamera,PCFShadowMap,Scene,SRGBColorSpace,Vector3,WebGLRenderer} from 'three';
import type {Participant,Position,RoomId} from '../../../../shared/plaza/types';
import {createAssets} from './assets';
import {createAvatar} from './avatar';
import {facingToRotation,interpolatePosition,toWorld} from './coordinates';
import {createScenery} from './scenery';

export type Frame={people:Array<{id:string;x:number;y:number;worldX:number;worldY:number}>;doors:Array<{roomId:RoomId;x:number;y:number}>;fps:number};
type Walker={model:ReturnType<typeof createAvatar>;position:Position;target:Participant;connected:boolean};

export function createRuntime(host:HTMLElement,roomId:RoomId,onFrame:(frame:Frame)=>void,onFailure:()=>void){
 const assets=createAssets();const scene=new Scene();scene.background=new Color('#e5edd9');
 const camera=new OrthographicCamera(-11,11,8,-8,0.1,100);camera.position.set(0,22,18);camera.lookAt(0,0.1,0);
 const people=new Group();scene.add(people);
 const walkers=new Map<string,Walker>();let renderer:WebGLRenderer|undefined,observer:ResizeObserver|undefined,sun:DirectionalLight|undefined;
 let disposed=false,width=1,height=1,previous=0,start=0;
 let scenery:ReturnType<typeof createScenery>;
 const point=new Vector3();
 const project=(position:Vector3)=>{point.copy(position).project(camera);return {x:(point.x+1)*width/2,y:(1-point.y)*height/2};};
 const dispose=()=>{
  if(disposed)return;disposed=true;
  renderer?.setAnimationLoop(null);observer?.disconnect();
  document.removeEventListener('visibilitychange',visibility);
  renderer?.domElement.removeEventListener('webglcontextlost',lost);
  sun?.shadow.dispose();assets.dispose();
  renderer?.dispose();renderer?.forceContextLoss();renderer?.domElement.remove();
  walkers.clear();scene.clear();
 };
 const failure=()=>{if(disposed)return;dispose();onFailure();};
 const lost=(event:Event)=>{event.preventDefault();failure();};
 const draw=(now:number)=>{
  if(disposed || !renderer)return;
  try{
   const dt=previous?Math.min(now-previous,100):16;previous=now;if(!start)start=now;
   const labels:Frame['people']=[];
   for(const [id,walker] of walkers){
    const next=interpolatePosition(walker.position,walker.target,dt);
    const distance=Math.hypot(next.x-walker.position.x,next.y-walker.position.y);
    walker.position=next;const world=toWorld(next);
    walker.model.group.position.set(world.x,world.y,world.z);
    walker.model.group.rotation.y=facingToRotation(walker.target.facing);
    walker.model.animate(distance,(now-start)/1000);
    labels.push({id,...project(new Vector3(world.x,1.55,world.z)),worldX:next.x,worldY:next.y});
   }
   renderer.render(scene,camera);
   onFrame({people:labels,doors:scenery.doors.map(d=>({roomId:d.roomId,...project(d.position)})),fps:dt>0?1000/dt:0});
  }catch{failure();}
 };
 const visibility=()=>{if(disposed)return;previous=0;renderer?.setAnimationLoop(document.hidden?null:draw);};
 try{
  renderer=new WebGLRenderer({antialias:true,alpha:false,powerPreference:'high-performance'});
  renderer.outputColorSpace=SRGBColorSpace;
  renderer.toneMapping=ACESFilmicToneMapping;renderer.toneMappingExposure=0.85;
  renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1,1.5));
  renderer.shadowMap.enabled=true;renderer.shadowMap.type=PCFShadowMap;
  renderer.domElement.setAttribute('role','img');renderer.domElement.setAttribute('aria-label',`${roomId==='lobby'?'ミライフ広場':({'cafe':'交流カフェ','hobby':'趣味の部屋','learning':'学びの部屋','sharing':'経験共有の部屋'}[roomId])}の3Dマップ`);
  renderer.domElement.addEventListener('webglcontextlost',lost);host.appendChild(renderer.domElement);
  scene.add(new HemisphereLight('#fff8e5','#aac59d',2));
  sun=new DirectionalLight('#fff0d5',2.5);sun.position.set(-8,16,9);sun.castShadow=true;
  sun.shadow.mapSize.set(1024,1024);sun.shadow.camera.left=-13;sun.shadow.camera.right=13;sun.shadow.camera.top=13;sun.shadow.camera.bottom=-13;
  sun.shadow.camera.near=0.1;sun.shadow.camera.far=45;sun.shadow.bias=-0.001;sun.shadow.normalBias=0.035;scene.add(sun);
  scenery=createScenery(roomId,assets);scene.add(scenery.group);
  const resize=()=>{
   if(disposed || !renderer)return;
   width=Math.max(host.clientWidth,1);height=Math.max(host.clientHeight,1);const aspect=width/height;
   const halfHeight=Math.max(7.3,11.2/aspect);
   camera.left=-halfHeight*aspect;camera.right=halfHeight*aspect;camera.top=halfHeight;camera.bottom=-halfHeight;
   camera.updateProjectionMatrix();renderer.setSize(width,height);previous=0;
  };
  resize();observer=new ResizeObserver(resize);observer.observe(host);
  document.addEventListener('visibilitychange',visibility);visibility();
 }catch(error){dispose();throw error;}
 return {
  update(participants:Participant[],selfId:string){
   if(disposed)return;
   const present=new Set(participants.filter(p=>p.roomId===roomId).map(p=>p.id));
   for(const [id,w] of walkers)if(!present.has(id)){people.remove(w.model.group);walkers.delete(id);}
   for(const p of participants){
    if(p.roomId!==roomId)continue;
    let walker=walkers.get(p.id);
    if(!walker){const model=createAvatar(p.avatar,p.id===selfId,assets);model.setConnected(p.connected);walker={model,position:{x:p.x,y:p.y},target:p,connected:p.connected};walkers.set(p.id,walker);people.add(model.group);}
    walker.target=p;
    if(walker.connected!==p.connected){walker.connected=p.connected;walker.model.setConnected(p.connected);}
   }
  },
  dispose,
 };
}
