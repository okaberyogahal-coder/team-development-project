import assert from 'node:assert/strict';
import test from 'node:test';
import {mkdtemp,cp,readFile,writeFile,rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {FileLocalDemoRepository} from '../../server/local-demo/repository';
import {loadLocalDemoState,buildStoredContext} from '../../server/local-demo/state';

async function setup(t:{after(fn:()=>Promise<void>):void}){
 const root=await mkdtemp(join(tmpdir(),'melife-local-demo-'));
 await cp('tests/fixtures/local-demo',root,{recursive:true});
 t.after(()=>rm(root,{recursive:true,force:true}));
 return {root,repo:new FileLocalDemoRepository(root)};
}
const now=Date.UTC(2026,9,24,12);

test('loads shared users, derives popularity from stored events and does not redate old records',async t=>{
 const {repo}=await setup(t);
 const state=await loadLocalDemoState(repo,'photography',now);
 assert.equal(state.users.length,3);assert.equal(state.currentUser?.nickname,'そら（架空）');
 assert.equal(state.popularity.activeUsers,2);
 assert.equal(state.popularity.items.find(x=>x.materialId==='natural-light')?.ratio,1);
 assert.equal(state.popularity.items.find(x=>x.materialId==='flower-arrange')?.ratio,0.5);
 assert.deepEqual(buildStoredContext(state).history.map(x=>x.id),['complete:natural-light','view:flower-arrange']);
 assert.equal((await loadLocalDemoState(repo,'newcomer',now+31*86400000)).popularity.activeUsers,0);
});

test('persists view and completion independently across repository instances without duplicate retries',async t=>{
 const {root,repo}=await setup(t);
 await repo.recordEvent('newcomer','digital','view',new Date(now));
 await repo.recordEvent('newcomer','digital','view',new Date(now+1));
 let user=await new FileLocalDemoRepository(root).getUser('newcomer');
 assert.equal(user.events.length,1);assert.equal(user.events[0].kind,'view');
 assert.equal(user.events[0].at,new Date(now+1).toISOString());
 await repo.recordEvent('newcomer','digital','complete',new Date(now+2));
 user=await repo.getUser('newcomer');
 assert.equal(user.events.length,2);assert.deepEqual(user.events.map(x=>x.id),['view:digital','complete:digital']);
});

test('serializes simultaneous writes across instances to preserve both events and changes revision',async t=>{
 const {root,repo}=await setup(t);
 const before=await loadLocalDemoState(repo,'newcomer',now);
 await Promise.all([repo.recordEvent('newcomer','digital','view',new Date(now)),new FileLocalDemoRepository(root).recordEvent('newcomer','cooking','view',new Date(now))]);
 const after=await loadLocalDemoState(repo,'newcomer',now);
 assert.equal(after.currentUser?.events.length,2);assert.notEqual(after.revision,before.revision);
});

test('rejects unknown paths/materials and never overwrites corrupt or missing data',async t=>{
 const {root,repo}=await setup(t);
 await assert.rejects(repo.getUser('../materials' as any));
 await assert.rejects(repo.recordEvent('newcomer','missing','view',new Date(now)));
 const path=join(root,'users/newcomer.json');await writeFile(path,'broken');
 await assert.rejects(repo.recordEvent('newcomer','digital','view',new Date(now)));
 assert.equal(await readFile(path,'utf8'),'broken');
 await rm(path);
 await assert.rejects(repo.recordEvent('newcomer','digital','view',new Date(now)));
 await assert.rejects(readFile(path));
});

test('rejects invalid event IDs, unknown catalog references and invalid timestamps',async t=>{
 const {root,repo}=await setup(t);const path=join(root,'users/newcomer.json');
 const user=await repo.getUser('newcomer');
 for(const event of [
  {id:'fake',materialId:'digital',kind:'view',at:new Date(now).toISOString()},
  {id:'view:missing',materialId:'missing',kind:'view',at:new Date(now).toISOString()},
  {id:'view:digital',materialId:'digital',kind:'view',at:'not a date'},
 ]){
  await writeFile(path,JSON.stringify({...user,events:[event]}));
  await assert.rejects(repo.getUser('newcomer'));
 }
});
