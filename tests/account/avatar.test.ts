import test from 'node:test';
import assert from 'node:assert/strict';
import {resolveAccountAvatar} from '../../shared/account-avatar';
import {DEFAULT_AVATAR} from '../../shared/demo-profile';

const stored = {version:1,id:'owner',nickname:'以前の名前',appearance:DEFAULT_AVATAR};
const storage = (value:unknown) => ({getItem:()=>JSON.stringify(value)});
test('account preview uses the saved wardrobe only for its owner and the current account name',()=>{
 assert.deepEqual(resolveAccountAvatar(storage(stored),'owner','現在の名前'),{...stored,nickname:'現在の名前'});
 assert.equal(resolveAccountAvatar(storage(stored),'other','別の人'),null);
});
test('missing and corrupt appearances have no account preview and never overwrite storage',()=>{
 for(const value of [null,{...stored,appearance:null},{...stored,appearance:{version:2}},{...stored,id:''}])
  assert.equal(resolveAccountAvatar(storage(value),'owner','現在の名前'),null);
 assert.equal(resolveAccountAvatar({getItem:()=>'{broken'},'owner','現在の名前'),null);
});
test('storage access failure remains distinguishable from an unset avatar',()=>{
 assert.throws(()=>resolveAccountAvatar({getItem:()=>{throw new Error('blocked');}},'owner','現在の名前'),/blocked/);
});
