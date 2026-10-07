import test from 'node:test';
import assert from 'node:assert/strict';
import {PlazaState} from '../../server/plaza/state';
import {RoomTransitions} from '../../server/plaza/transitions';
import type {CallGateway} from '../../server/plaza/calls';
const setup=()=>{const s=new PlazaState();const p=s.join({name:'花子',avatar:'mint'},0).session;return {s,p};};
function gateway(revoke:CallGateway['revoke']=async()=>{}) :CallGateway {return {configured:true,issue:async(p,g)=>({url:'wss://example',token:'fake',identity:`${p.id}:${g}:${p.visit}`,roomId:p.roomId,generation:g}),revoke,initialize:async()=>{}};}
test('旧通話の停止完了前に所属と新しい通話資格を変えない',async()=>{
 const {s,p}=setup();s.reserveRoom(p.id,'cafe','a');s.commitRoom('a');p.callIdentity='old';
 let finish!:()=>void;const t=new RoomTransitions(s,gateway(()=>new Promise<void>(r=>finish=r)));
 const pending=t.changeRoom(p.id,'hobby','b');assert.equal(p.roomId,'cafe');assert.equal(p.busy,true);
 await assert.rejects(t.changeRoom(p.id,'learning','c'),/BUSY/);finish();const result=await pending;
 assert.equal(p.roomId,'hobby');assert.equal(result.credentials?.roomId,'hobby');assert.equal(p.busy,false);
});
test('通話失効失敗では移動を確定せず移動先予約を解放',async()=>{
 const {s,p}=setup();s.reserveRoom(p.id,'cafe','a');s.commitRoom('a');p.callIdentity='old';
 const t=new RoomTransitions(s,gateway(async()=>{throw Error('provider offline');}));
 await assert.rejects(t.changeRoom(p.id,'hobby','b'));assert.equal(p.roomId,'cafe');assert.equal(s.summaries().find(r=>r.id==='hobby')?.count,0);
});
test('新しい資格取得失敗ではロビーに戻り、ロビーでは資格を発行しない',async()=>{
 const {s,p}=setup();const g=gateway();g.issue=async()=>{throw Error('failed');};const t=new RoomTransitions(s,g);
 const result=await t.changeRoom(p.id,'cafe','a');assert.equal(p.roomId,'lobby');assert.equal(result.error,'CALL_FAILED');
 assert.equal((await t.retryCall(p.id)).credentials,null);
});
test('旧接続の完了が遅くても切断後に新資格を渡さない',async()=>{
 const {s,p}=setup();s.reserveRoom(p.id,'cafe','a');s.commitRoom('a');p.callIdentity='old';
 let finish!:()=>void;const t=new RoomTransitions(s,gateway(()=>new Promise<void>(r=>finish=r)));
 const pending=t.changeRoom(p.id,'hobby','b');s.disconnect(p.id,p.generation);finish();
 await assert.rejects(pending,/EXPIRED/);assert.equal(p.roomId,'cafe');assert.equal(s.summaries().find(r=>r.id==='hobby')?.count,0);
});
test('未設定でも部屋移動でき、通話成功を返さない',async()=>{
 const {s,p}=setup();const g=gateway();g.configured=false;const result=await new RoomTransitions(s,g).changeRoom(p.id,'cafe','a');assert.equal(p.roomId,'cafe');assert.equal(result.credentials,null);assert.equal(result.error,'CALL_UNCONFIGURED');
});
test('旧資格発行の遅い成功が復帰後の所属・新資格を変更しない',async()=>{
 const {s,p}=setup();let finish!:()=>void;let started!:()=>void;const ready=new Promise<void>(r=>started=r);const revoked:{roomId:string;identity:string}[]=[];let issued=0;
 const g=gateway(async(roomId,identity)=>{revoked.push({roomId,identity});});
 g.issue=async(snapshot,generation)=>{const index=++issued;if(index===1){started();await new Promise<void>(r=>finish=r);}return {url:'wss://example',token:'fake',identity:index===1?'old':'new',roomId:snapshot.roomId,generation};};
 const t=new RoomTransitions(s,g);const old=t.changeRoom(p.id,'cafe','a');void old.catch(()=>{});await ready;s.disconnect(p.id,1);s.join({name:'花子',avatar:'mint',resumeToken:p.token});
 await t.changeRoom(p.id,'hobby','b');finish();await assert.rejects(old,/EXPIRED/);
 assert.equal(p.roomId,'hobby');assert.equal(p.callIdentity,'new');assert.deepEqual(revoked,[{roomId:'cafe',identity:'old'}]);
});
test('旧資格発行の失敗が復帰後の参加者をロビーへ移動しない',async()=>{
 const {s,p}=setup();let fail!:(e:Error)=>void;let started!:()=>void;const ready=new Promise<void>(r=>started=r);let issued=0;const g=gateway();g.issue=async(snapshot,generation)=>{if(++issued===1){started();await new Promise<void>((_,reject)=>fail=reject);}return {url:'x',token:'x',identity:'new',roomId:snapshot.roomId,generation};};
 const t=new RoomTransitions(s,g);const old=t.changeRoom(p.id,'cafe','a');void old.catch(()=>{});await ready;s.disconnect(p.id,1);s.join({name:'花子',avatar:'mint',resumeToken:p.token});await t.changeRoom(p.id,'hobby','b');fail(Error('old failure'));await assert.rejects(old,/EXPIRED/);assert.equal(p.roomId,'hobby');assert.equal(p.callIdentity,'new');
});
test('資格発行中の退出で遅い結果を返さず、その部屋の資格を失効する',async()=>{
 const {s,p}=setup();let finish!:()=>void;let started!:()=>void;const ready=new Promise<void>(r=>started=r);const revoked:string[]=[];const g=gateway(async(room,identity)=>{revoked.push(`${room}/${identity}`);});g.issue=async(snapshot,generation)=>{started();await new Promise<void>(r=>finish=r);return {url:'x',token:'x',identity:'late',roomId:snapshot.roomId,generation};};
 const old=new RoomTransitions(s,g).changeRoom(p.id,'cafe','a');void old.catch(()=>{});await ready;s.leave(p.id);finish();await assert.rejects(old,/EXPIRED/);assert.deepEqual(revoked,['cafe/late']);assert.equal(s.total,0);
});
