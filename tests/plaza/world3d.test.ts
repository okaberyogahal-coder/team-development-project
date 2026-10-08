import {test} from 'node:test';
import assert from 'node:assert/strict';
import {toWorld,interpolatePosition,facingToRotation} from '../../app/components/plaza/world3d/coordinates';

test('shared coordinates map to the floor without reversing movement',()=>{
 assert.deepEqual(toWorld({x:500,y:350}),{x:0,y:0,z:0});
 assert.deepEqual(toWorld({x:0,y:0}),{x:-10,y:0,z:-7});
 assert.deepEqual(toWorld({x:1000,y:700}),{x:10,y:0,z:7});
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
