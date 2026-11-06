import {test} from 'node:test';
import assert from 'node:assert/strict';
import {resolveWorldAvatar} from '../../shared/world-avatar';
import {DEFAULT_AVATAR,DEMO_PROFILE_KEY} from '../../shared/demo-profile';
test('world resolves completed appearance only for the verified owner, never drafts or guest',()=>{
 const profile={version:1,id:'owner-a',nickname:'A',appearance:DEFAULT_AVATAR};
 const store={getItem:(key:string)=>key===DEMO_PROFILE_KEY?JSON.stringify(profile):null};
 assert.equal(resolveWorldAvatar(store,null),null);
 assert.equal(resolveWorldAvatar(store,'owner-b'),null);
 assert.deepEqual(resolveWorldAvatar(store,'owner-a'),profile);
});
