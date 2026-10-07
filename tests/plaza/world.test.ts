import test from 'node:test';
import assert from 'node:assert/strict';
import { parseClientMessage } from '../../shared/plaza/validation';
import { advance } from '../../shared/plaza/movement';

test('名前を正規化し空白・長すぎる名前・未知のアバターを拒否する', () => {
  assert.deepEqual(parseClientMessage(JSON.stringify({type:'join',name:'  花子  ',avatar:'coral'})), {type:'join',name:'花子',avatar:'coral'});
  for (const name of ['', '  ', 'あ'.repeat(21)]) assert.equal(parseClientMessage(JSON.stringify({type:'join',name,avatar:'coral'})), null);
  assert.equal(parseClientMessage('{broken'), null);
  assert.equal(parseClientMessage(JSON.stringify({type:'join',name:'花子',avatar:'unknown'})), null);
});
test('不正な移動方向・未知の部屋・大きい入力を拒否する', () => {
  for (const x of [null, 2, '1']) assert.equal(parseClientMessage(JSON.stringify({type:'move',x,y:0,seq:1})), null);
  assert.equal(parseClientMessage(JSON.stringify({type:'changeRoom',roomId:'secret',requestId:'a'})), null);
  assert.equal(parseClientMessage('x'.repeat(4097)), null);
});
test('斜め移動は単軸と同速で、停止入力では位置が変わらない', () => {
  const p = {x:500,y:350};
  const a = advance(p,{x:1,y:0},100,'lobby');
  const b = advance(p,{x:1,y:1},100,'lobby');
  assert.equal(a.x,512);
  assert.ok(Math.abs(Math.hypot(b.x-p.x,b.y-p.y)-12)<0.001);
  assert.deepEqual(advance(p,{x:0,y:0},100,'lobby'),p);
});
test('マップ境界と壁の中へ移動できない', () => {
  assert.equal(advance({x:974,y:650},{x:1,y:0},100,'lobby').x,974);
  assert.deepEqual(advance({x:245,y:120},{x:1,y:0},100,'lobby'),{x:245,y:120});
});
