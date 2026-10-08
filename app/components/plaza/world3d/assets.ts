import {BoxGeometry,BufferGeometry,ConeGeometry,CylinderGeometry,Float32BufferAttribute,Mesh,MeshStandardMaterial,SphereGeometry,TorusGeometry} from 'three';

export type Shape='box'|'sphere'|'cylinder'|'cone'|'ring'|'roof';
function roofGeometry(){
 const geometry=new BufferGeometry();
 geometry.setAttribute('position',new Float32BufferAttribute([-.5,-.5,-.5,.5,-.5,-.5,.5,-.5,.5,-.5,-.5,.5,-.5,.5,0,.5,.5,0],3));
 geometry.setIndex([0,3,4,1,5,2,0,5,1,0,4,5,3,5,4,3,2,5,0,2,3,0,1,2]);
 const flat=geometry.toNonIndexed();geometry.dispose();flat.computeVertexNormals();return flat;
}
export function createAssets(){
 const geometries=new Map<Shape,BufferGeometry>();
 const materials=new Map<string,MeshStandardMaterial>();
 let disposed=false;
 return {
  geometry(name:Shape){
   let value=geometries.get(name);
   if(!value){
    value=name==='roof'?roofGeometry():name==='box'?new BoxGeometry(1,1,1):name==='sphere'?new SphereGeometry(1,16,12):name==='cylinder'?new CylinderGeometry(1,1,1,20):name==='cone'?new ConeGeometry(1,1,4):new TorusGeometry(1,0.07,8,32);
    geometries.set(name,value);
   }
   return value;
  },
  material(color:string,opacity=1){
   const key=`${color}:${opacity}`;let value=materials.get(key);
   if(!value){value=new MeshStandardMaterial({color,roughness:0.85,transparent:opacity<1,opacity});materials.set(key,value);}
   return value;
  },
  dispose(){if(disposed)return;disposed=true;geometries.forEach(g=>g.dispose());materials.forEach(m=>m.dispose());geometries.clear();materials.clear();},
 };
}
export type Assets=ReturnType<typeof createAssets>;
export function shape(assets:Assets,kind:Shape,color:string,position:number[],scale:number[]){
 const mesh=new Mesh(assets.geometry(kind),assets.material(color));
 mesh.position.set(position[0],position[1],position[2]);mesh.scale.set(scale[0],scale[1],scale[2]);
 mesh.castShadow=true;mesh.receiveShadow=true;return mesh;
}
