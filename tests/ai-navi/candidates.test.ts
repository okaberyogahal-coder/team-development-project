import assert from 'node:assert/strict';
import test from 'node:test';
import { selectCandidates } from '../../server/ai-navi/candidates';
import { aggregatePopularity } from '../../server/ai-navi/popularity';
import type { Material, PersonalInput } from '../../server/ai-navi/types';

const now=Date.UTC(2026,9,23);
const materials:Material[]=[
 {id:'camera',title:'Camera',description:'',topics:['写真'],published:true},
 {id:'flowers',title:'Flowers',description:'',topics:['写真','生花'],published:true},
 {id:'popular',title:'Popular',description:'',topics:['料理'],published:true},
 {id:'private',title:'Private',description:'',topics:['写真'],published:false},
];
const empty:PersonalInput={interests:[],goals:[],events:[],feedback:[]};
const popularity=aggregatePopularity(materials,[{userId:'other',materialId:'popular',kind:'complete',at:now}],now);

test('registered interests and goals outrank global popularity',()=>{
 for(const personal of [{...empty,interests:['写真']},{...empty,goals:['写真を学ぶ']}]){
  const result=selectCandidates(materials,personal,popularity);
  assert.deepEqual(result.map(x=>x.materialId),['camera','flowers','popular']);
  assert.deepEqual(result[0].sources,['interest']);
 }
});

test('viewed or completed topics lead to related material without changing completion history',()=>{
 for(const kind of ['view','complete'] as const){
  const personal:PersonalInput={...empty,events:[{userId:'me',materialId:'camera',kind,at:now}]};
  const before=structuredClone(personal);
  const result=selectCandidates(materials,personal,popularity);
  assert.ok(result.findIndex(x=>x.materialId==='flowers') < result.findIndex(x=>x.materialId==='popular'));
  assert.ok(result.find(x=>x.materialId==='flowers')?.sources.includes('history'));
  assert.deepEqual(personal,before);
 }
});

test('first-time users get popularity, or ordinary catalog recommendations when nobody has used materials',()=>{
 const result=selectCandidates(materials,empty,popularity);
 assert.equal(result[0].materialId,'popular');
 assert.deepEqual(result[0].sources,['popularity']);
 const noHistory=selectCandidates(materials,empty,aggregatePopularity(materials,[],now));
 assert.deepEqual(noHistory.map(x=>x.materialId),['camera','flowers','popular']);
 assert.ok(noHistory.every(x=>x.sources.length===1&&x.sources[0]==='catalog'));
});

test('feedback affects selection, and withdrawal restores a candidate',()=>{
 const blocked=selectCandidates(materials,{...empty,feedback:[{materialId:'popular',value:'not-interested'}]},popularity);
 assert.ok(!blocked.some(x=>x.materialId==='popular'));
 assert.equal(selectCandidates(materials,empty,popularity)[0].materialId,'popular');
 const liked=selectCandidates(materials,{...empty,feedback:[{materialId:'flowers',value:'interested'}]},popularity);
 assert.equal(liked[0].materialId,'flowers');
 assert.deepEqual(liked[0].sources,['feedback']);
});

test('excludes newly private materials even when an old aggregate calls them popular',()=>{
 const unpublished=materials.map(x=>({...x,published:x.id==='popular'?false:x.published}));
 assert.ok(!selectCandidates(unpublished,empty,popularity).some(x=>['private','popular'].includes(x.materialId)));
});

test('normalizes full-width and casing, ignoring blank terms rather than matching everything',()=>{
 const catalog=[{...materials[0],topics:['AI']}];
 assert.deepEqual(selectCandidates(catalog,{...empty,interests:[' ａｉ ']},popularity)[0].sources,['interest']);
 assert.deepEqual(selectCandidates(catalog,{...empty,interests:['   ']},popularity)[0].sources,['catalog']);
});

test('limits candidates to 20 without leaking user identities',()=>{
 const catalog=Array.from({length:25},(_,i)=>({...materials[0],id:String(i).padStart(2,'0')}));
 const result=selectCandidates(catalog,empty,aggregatePopularity(catalog,[],now));
 assert.equal(result.length,20);
 assert.deepEqual(result[19],{materialId:'19',sources:['catalog']});
});
