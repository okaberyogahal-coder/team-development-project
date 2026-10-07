import test from 'node:test';
import assert from 'node:assert/strict';
import { PlazaState } from '../../server/plaza/state';
const join=(s:PlazaState)=>s.join({name:'花子',avatar:'coral'},0);
test('50人の後は拒否し、退出で全体枠を解放する',()=>{
 const s=new PlazaState();const ids=[];
 for(let i=0;i<50;i++) ids.push(join(s).session.id);
 assert.throws(()=>join(s),/FULL/);s.leave(ids[0]);assert.equal(join(s).session.roomId,'lobby');assert.equal(s.total,50);
});
test('同時入室でも8人のみ予約でき、全体人数は二重計上しない',()=>{
 const s=new PlazaState();const people=Array.from({length:9},()=>join(s).session);
 people.slice(0,8).forEach((p,i)=>s.reserveRoom(p.id,'cafe',`r${i}`,0));
 assert.throws(()=>s.reserveRoom(people[8].id,'cafe','r9',0),/ROOM_FULL/);
 assert.equal(s.total,9);assert.equal(s.summaries().find(r=>r.id==='cafe')?.count,8);
 s.cancelReservation('r0');assert.doesNotThrow(()=>s.reserveRoom(people[8].id,'cafe','r9',0));
});
test('10秒で予約が失効し、遅い完了が所属を変更しない',()=>{
 const s=new PlazaState();const p=join(s).session;s.reserveRoom(p.id,'cafe','r',0);s.tick(10001,50);
 assert.throws(()=>s.commitRoom('r',10001),/EXPIRED/);assert.equal(p.roomId,'lobby');assert.equal(s.summaries().find(r=>r.id==='cafe')?.count,0);
});
test('500ms以上入力が途絶すると停止する',()=>{
 const s=new PlazaState();const p=join(s).session;s.move(p.id,{x:1,y:0},1,0);s.tick(100,50);const x=p.x;s.tick(600,50);assert.equal(p.x,x);
});
test('参加直後のアバターが重ならないよう出現位置を分ける',()=>{
 const s=new PlazaState();const a=join(s).session;const b=join(s).session;
 assert.ok(Math.hypot(a.x-b.x,a.y-b.y)>=35);
});
test('50人の入場でもアバターの出現位置を重ねない',()=>{
 const s=new PlazaState();const people=Array.from({length:50},()=>join(s).session);
 assert.equal(new Set(people.map(p=>`${p.x},${p.y}`)).size,50);
 for(let i=0;i<50;i++)for(let j=0;j<i;j++)assert.ok(Math.hypot(people[i].x-people[j].x,people[i].y-people[j].y)>=35);
});
