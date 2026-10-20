import test from 'node:test';
import assert from 'node:assert/strict';
import { createDemoProfile, readDemoProfile, saveDemoProfile, updateDemoAvatar, DEMO_PROFILE_KEY } from '../../shared/demo-profile';

function storage() {
 const values = new Map<string,string>();
 return {getItem:(key:string)=>values.get(key) ?? null,setItem:(key:string,value:string)=>{values.set(key,value);},removeItem:(key:string)=>{values.delete(key);}};
}
test('体験登録は名前だけを保存し、パスワードやメールを保存しない',()=>{
 const store=storage();
 const profile=createDemoProfile('  みらい  ', 'demo-1');
 saveDemoProfile(store,profile);
 assert.equal(readDemoProfile(store)?.nickname,'みらい');
 assert.equal(readDemoProfile(store)?.appearance,null);
 assert.deepEqual(Object.keys(JSON.parse(store.getItem(DEMO_PROFILE_KEY)!)).sort(),['appearance','id','nickname','version']);
});
test('Unityで作成した外見を同じプロフィールに保存して再開できる',()=>{
 const store=storage();const profile=createDemoProfile('みらい','demo-1');
 saveDemoProfile(store,profile);
 const appearance={version:1,baseVariant:3,parts:[2,1,2,0,1,1],colors:[1,2,5,7,3,6]};
 assert.equal(updateDemoAvatar(store,{profileId:'other',nickname:'別人',appearance}),false);
 assert.equal(updateDemoAvatar(store,{profileId:'demo-1',nickname:'みらい',appearance}),true);
 assert.deepEqual(readDemoProfile(store)?.appearance,appearance);
 assert.equal(readDemoProfile(store)?.id,profile.id);
});
test('壊れた保存データや不正なUnity外見を受け入れない',()=>{
 const store=storage();store.setItem(DEMO_PROFILE_KEY,'invalid');assert.equal(readDemoProfile(store),null);
 saveDemoProfile(store,createDemoProfile('みらい','demo-1'));
 assert.equal(updateDemoAvatar(store,{profileId:'demo-1',nickname:'みらい',appearance:{version:1,baseVariant:0,parts:[999],colors:[]}}),false);
 assert.equal(readDemoProfile(store)?.appearance,null);
});
test('Unityと同じ名前の正規化を使い、空になる名前は登録しない',()=>{
 assert.equal(createDemoProfile('<みらい>','demo-1').nickname,'みらい');
 assert.throws(()=>createDemoProfile('<>','demo-1'));
});
test('保存機能を利用できない場合は、データなしと区別して呼び出し元に通知する',()=>{
 assert.throws(()=>readDemoProfile({getItem(){throw new Error('blocked');},setItem(){}}),/blocked/);
});
