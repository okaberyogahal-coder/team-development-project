import {test} from 'node:test';
import assert from 'node:assert/strict';
import {toWorld,interpolatePosition,facingToRotation} from '../../app/components/plaza/world3d/coordinates';
import {createAssets} from '../../app/components/plaza/world3d/assets';
import {createScenery} from '../../app/components/plaza/world3d/scenery';
import {createAvatar} from '../../app/components/plaza/world3d/avatar';
import {Mesh} from 'three';

test('shared coordinates map to the floor without reversing movement',()=>{
 assert.deepEqual(toWorld({x:500,y:350}),{x:0,y:0,z:0});
 assert.deepEqual(toWorld({x:0,y:0}),{x:-10,y:0,z:-7});
 assert.deepEqual(toWorld({x:1000,y:700}),{x:10,y:0,z:7});
});

test('shared GPU assets are released exactly once across room changes',()=>{
 const assets=createAssets();const geometry=assets.geometry('sphere');const material=assets.material('#ffffff');
 assert.equal(assets.geometry('sphere'),geometry);assert.equal(assets.material('#ffffff'),material);
 let geometries=0,materials=0;
 geometry.addEventListener('dispose',()=>geometries++);material.addEventListener('dispose',()=>materials++);
 assets.dispose();assets.dispose();assert.equal(geometries,1);assert.equal(materials,1);
});
test('lobby doors connect to all four rooms, and every interior has an exit',()=>{
 const assets=createAssets();
 const lobby=createScenery('lobby',assets);
 assert.deepEqual(lobby.doors.map(d=>d.roomId),['cafe','hobby','learning','sharing']);
 assert.ok(lobby.group.children.length>10);
 for(const id of ['cafe','hobby','learning','sharing'] as const){
  const room=createScenery(id,assets);assert.deepEqual(room.doors.map(d=>d.roomId),['lobby']);
  assert.ok(room.group.children.length>5);
 }
 assets.dispose();
});
test('all avatar colors support walking, idle, and reconnect appearance',()=>{
 const assets=createAssets();
 for(const id of ['coral','sky','mint','violet','sun'] as const){
  const avatar=createAvatar(id,id==='mint',assets);
  const leg=avatar.group.getObjectByName('left-leg')!;
  avatar.animate(1,0.2);assert.notEqual(leg.rotation.x,0);
  avatar.animate(0,0.3);assert.equal(leg.rotation.x,0);
  avatar.setConnected(false);
  const shirt=avatar.group.getObjectByName('shirt') as Mesh;
  assert.equal((shirt.material as any).opacity,0.45);
  avatar.setConnected(true);assert.equal((shirt.material as any).opacity,1);
 }
 assets.dispose();
});
test('interpolation stops at its target and ignores negative elapsed time',()=>{
 assert.deepEqual(interpolatePosition({x:0,y:0},{x:100,y:200},50),{x:50,y:100});
 assert.deepEqual(interpolatePosition({x:0,y:0},{x:100,y:200},-1),{x:0,y:0});
 assert.deepEqual(interpolatePosition({x:0,y:0},{x:100,y:200},200),{x:100,y:200});
});
test('avatar forward points in the server facing direction',()=>{
 for(const [facing,want] of [[0,Math.PI/2],[Math.PI/2,0],[Math.PI,-Math.PI/2],[-Math.PI/2,Math.PI]]){
  assert.ok(Math.abs(facingToRotation(facing)-want)<1e-10);
 }
});
