import assert from 'node:assert/strict';
import {test} from 'node:test';
import {signupInput} from '../../shared/auth/validation';
import {ageBadge, demographicInput, worldProfileReady} from '../../shared/profile-demographics';

test('age badges use the approved adult boundaries',()=>{
 for(const [age,badge] of [[18,'ユース'],[29,'ユース'],[30,'ミドル'],[49,'ミドル'],[50,'ミドルシニア'],[59,'ミドルシニア'],[60,'シニア'],[130,'シニア']] as const) assert.equal(ageBadge(age),badge);
 for(const age of [null,undefined,0,17,18.5,NaN,Infinity,131]) assert.equal(ageBadge(age),null);
});
test('gender and an adult integer age are required',()=>{
 for(const gender of ['male','female']) assert.deepEqual(demographicInput({gender,age:18}),{gender,age:18});
 for(const gender of [undefined,null,'','other',1]) assert.throws(()=>demographicInput({gender,age:30}));
 for(const age of [undefined,null,'18',17,18.5,NaN,131]) assert.throws(()=>demographicInput({gender:'male',age}));
});

test('signup enforces demographics even with adult consent',()=>{
 const input={email:'adult@example.invalid',password:'long-enough-password',confirmation:'long-enough-password',adult:true,terms:true,privacy:true,nickname:'利用者',avatar:'cat',interests:[],teaches:[],wantsToLearn:[],strengths:'',goal:'',gender:'female',age:60};
 assert.equal(signupInput(input).gender,'female');
 for(const change of [{gender:undefined},{gender:''},{age:undefined},{age:17},{age:18.1}]) assert.throws(()=>signupInput({...input,...change}));
});

test('direct world sessions require a completed adult profile',()=>{
 assert.equal(worldProfileReady({gender:'male',age:18}),true);
 assert.equal(worldProfileReady({gender:'female',age:60}),true);
 for(const profile of [null,undefined,{}, {gender:null,age:30},{gender:'female',age:null},{gender:'male',age:17},{gender:'other',age:30}]) assert.equal(worldProfileReady(profile),false);
});
