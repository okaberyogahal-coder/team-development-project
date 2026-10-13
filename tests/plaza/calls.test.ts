import test from 'node:test';
import assert from 'node:assert/strict';
import {TokenVerifier} from 'livekit-server-sdk';
import {LiveKitCalls} from '../../server/plaza/calls';
test('通話資格を現在の部屋・参加者・カメラとマイクに限定する',async()=>{
 const calls=new LiveKitCalls('wss://test.livekit.cloud','test-key','test-secret-test-secret-test-secret','test-prefix');
 const c=await calls.issue({id:'p',name:'花子',avatar:'mint',roomId:'cafe',x:0,y:0,facing:0,connected:true,visit:3},2);
 const token=await new TokenVerifier('test-key','test-secret-test-secret-test-secret').verify(c.token);
 assert.equal(token.sub,'p:2:3');assert.equal(token.video?.room,'test-prefix-cafe');assert.equal(token.video?.roomJoin,true);assert.equal(token.video?.canPublishData,false);assert.deepEqual(token.video?.canPublishSources,['camera','microphone']);assert.equal(token.video?.roomAdmin,undefined);
});
test('ロビーの資格発行とself-hosted接続先を拒否する',async()=>{
 assert.throws(()=>new LiveKitCalls('ws://localhost:7880','a','b','test'));
 const calls=new LiveKitCalls('wss://test.livekit.cloud','a','b','test');await assert.rejects(calls.issue({id:'p',name:'a',avatar:'sky',roomId:'lobby',x:0,y:0,facing:0,connected:true,visit:1},1),/NO_CALL/);
});
test('LiveKit Cloudの初期化に十分なリクエストタイムアウトを使う',()=>{
 const calls=new LiveKitCalls('wss://test.livekit.cloud','a','b','test');
 assert.ok((calls as unknown as {api:{rpc:{requestTimeout:number}}}).api.rpc.requestTimeout>=20);
});
