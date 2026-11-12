import {test} from 'node:test';
import assert from 'node:assert/strict';
import {previewAccepted} from '../../shared/avatar-v3-preview';
import {DEFAULT_AVATAR_CHOICE} from '../../shared/avatar-types';
const choice={...DEFAULT_AVATAR_CHOICE,baseAvatarId:'young-man'};
test('only the current accepted model/face pair enables completion',()=>{
 const receipt={type:'miraifu:avatar-state',revision:4,state:{accepted:true,pairMatched:true,choice}};
 assert.equal(previewAccepted(receipt,4,choice),true);
 assert.equal(previewAccepted(receipt,5,choice),false);
 assert.equal(previewAccepted(receipt,4,{...choice,face:1}),false);
 assert.equal(previewAccepted({...receipt,state:{...receipt.state,pairMatched:false}},4,choice),false);
 assert.equal(previewAccepted({...receipt,state:{...receipt.state,accepted:false}},4,choice),false);
 assert.equal(previewAccepted({type:'miraifu:avatar-ready'},4,choice),false);
});
