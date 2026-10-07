import test from 'node:test';
import assert from 'node:assert/strict';
import {CallSession,CallPort} from '../../app/components/plaza/call-state';
import type {CallCredentials} from '../../shared/plaza/types';
const credentials=(roomId:'cafe'|'hobby'):CallCredentials=>({url:'wss://example',token:'fake',identity:roomId,roomId,generation:1});
const port=():CallPort=>({connect:async()=>{},close:async()=>{},setMic:async()=>{},setCamera:async()=>{}});
test('部屋変更後の古い接続結果が現在の状態を上書きしない',async()=>{
 let finish!:()=>void;let closed=0;const old=port();old.connect=()=>new Promise<void>(r=>finish=r);old.close=async()=>{closed++;};
 const session=new CallSession(()=>{});const pending=session.connect(credentials('cafe'),old);await session.connect(credentials('hobby'),port());finish();await pending;
 assert.equal(session.snapshot.status,'connected');assert.equal(session.snapshot.roomId,'hobby');assert.equal(closed>0,true);
});
test('機器オン失敗時に表示をオンにせず、終了で両機器をオフにする',async()=>{
 const p=port();p.setMic=async()=>{throw Error('denied');};const s=new CallSession(()=>{});await s.connect(credentials('cafe'),p);await s.toggleMic();assert.equal(s.snapshot.micEnabled,false);assert.ok(s.snapshot.error);
 await s.toggleCamera();assert.equal(s.snapshot.cameraEnabled,true);await s.disconnect();assert.equal(s.snapshot.cameraEnabled,false);assert.equal(s.snapshot.micEnabled,false);assert.equal(s.snapshot.status,'idle');
});
test('機器操作中に退出しても遅い成功でオンにならない',async()=>{
 let finish!:()=>void;const p=port();p.setCamera=()=>new Promise<void>(r=>finish=r);const s=new CallSession(()=>{});await s.connect(credentials('cafe'),p);const pending=s.toggleCamera();await s.disconnect();finish();await pending;assert.equal(s.snapshot.cameraEnabled,false);assert.equal(s.snapshot.status,'idle');
});
test('復帰後に資格がなくても部屋で通話を再試行できる',async()=>{
 const {canRetryCall}=await import('../../app/components/plaza/call-state');
 assert.equal(canRetryCall(true,false,'idle',false),true);
 assert.equal(canRetryCall(true,true,'idle',false),false);
 assert.equal(canRetryCall(false,false,'idle',false),false);
 assert.equal(canRetryCall(true,false,'connecting',true),false);
});
