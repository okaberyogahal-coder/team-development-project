import test from 'node:test';
import assert from 'node:assert/strict';
import {PlazaState} from '../../server/plaza/state';
test('30秒以内に同じID・部屋・位置へ復帰し、古いcloseは新しい接続を消さない',()=>{
 const s=new PlazaState();const p=s.join({name:'花子',avatar:'mint'},0).session;s.reserveRoom(p.id,'cafe','a',0);s.commitRoom('a',1);p.x=600;
 s.disconnect(p.id,1,100);const resumed=s.join({name:'花子',avatar:'mint',resumeToken:p.token},20000).session;
 assert.equal(resumed.id,p.id);assert.equal(resumed.roomId,'cafe');assert.equal(resumed.x,600);assert.equal(s.total,1);
 s.disconnect(p.id,1,20001);assert.equal(resumed.connected,true);assert.equal(resumed.generation,2);
});
test('30秒経過後に枠を解放し、再参加はロビーから新しいIDで開始する',()=>{
 const s=new PlazaState();const p=s.join({name:'花子',avatar:'mint'},0).session;s.disconnect(p.id,1,100);s.tick(30100,50);assert.equal(s.total,0);
 const newP=s.join({name:'花子',avatar:'mint',resumeToken:p.token},30101).session;assert.notEqual(newP.id,p.id);assert.equal(newP.roomId,'lobby');
});
test('切断で移動予約を解放し旧部屋枠のみ保持する',()=>{
 const s=new PlazaState();const p=s.join({name:'花子',avatar:'mint'},0).session;s.reserveRoom(p.id,'cafe','a',0);s.disconnect(p.id,1,1);
 assert.equal(s.summaries().find(r=>r.id==='cafe')?.count,0);assert.equal(s.total,1);
});
test('期限切れtokenで再参加する際に古い枠を二重に数えない',()=>{
 const s=new PlazaState();const p=s.join({name:'花子',avatar:'mint'},0).session;s.disconnect(p.id,1,1);
 s.join({name:'花子',avatar:'mint',resumeToken:p.token},31001);assert.equal(s.total,1);
});
