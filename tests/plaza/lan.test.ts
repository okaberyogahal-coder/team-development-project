import test from 'node:test';
import assert from 'node:assert/strict';
import {lanConfig} from '../../scripts/lan-config.mjs';
import {createRoomRequestId} from '../../shared/plaza/request-id';
test('このPC自身のLANアドレスだけを参加URLと許可Originに追加する',()=>{
 const config=lanConfig({en0:[{address:'10.77.98.69',family:'IPv4',internal:false}],lo0:[{address:'127.0.0.1',family:'IPv4',internal:true}],vpn:[{address:'203.0.113.2',family:'IPv4',internal:false}]},3002,3003);
 assert.deepEqual(config.urls,['http://10.77.98.69:3002/plaza']);
 assert.ok(config.origins.includes('http://10.77.98.69:3002'));assert.ok(config.origins.includes('http://localhost:3002'));
 assert.equal(config.origins.includes('http://203.0.113.2:3002'),false);
});
test('LANのHTTPでもrandomUUIDなしで部屋移動の要求IDを発行する',()=>{
 const cryptoLike={getRandomValues:<T extends ArrayBufferView|null>(array:T):T=>{if(array instanceof Uint8Array)array.fill(12);return array;}};
 assert.match(createRoomRequestId(cryptoLike),/^[a-zA-Z0-9-]{1,64}$/);
});
