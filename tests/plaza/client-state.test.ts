import test from 'node:test';
import assert from 'node:assert/strict';
import {initialClientState,receiveMessage,canMove} from '../../app/components/plaza/client-state';
import type {Participant,ServerMessage} from '../../shared/plaza/types';
const self:Participant={id:'a',name:'花子',avatar:'mint',roomId:'lobby',x:500,y:540,facing:0,connected:true};
const welcome:ServerMessage={type:'welcome',self,participants:[self],rooms:[],total:1,resumeToken:'abc',generation:2,callConfigured:false};
test('welcome前は移動できず、参加後に移動できる',()=>{
 assert.equal(canMove(initialClientState),false);assert.equal(canMove(receiveMessage(initialClientState,welcome)),true);
});
test('古い世代の状態・別部屋の資格を無視する',()=>{
 const s=receiveMessage(initialClientState,welcome);
 assert.deepEqual(receiveMessage(s,{type:'state',generation:1,participants:[],rooms:[],total:0}),s);
 assert.equal(receiveMessage(s,{type:'callCredentials',credentials:{url:'x',token:'x',identity:'a',roomId:'cafe',generation:2}}).credentials,null);
});
test('満員で操作停止し、welcomeで再参加できる',()=>{
 const full=receiveMessage(initialClientState,{type:'error',code:'FULL',message:'満員'});assert.equal(full.status,'full');assert.equal(canMove(full),false);
 assert.equal(receiveMessage(full,welcome).status,'joined');
});
