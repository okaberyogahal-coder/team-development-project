import assert from 'node:assert/strict';
import test from 'node:test';
import {mkdtemp,cp,rm,readFile} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {FileLocalDemoRepository} from '../../server/local-demo/repository';
import {createLocalFriendsHandler} from '../../server/local-demo/friends-handler';
import {LOCAL_SESSION_COOKIE,LocalSessionStore} from '../../server/local-demo/session';

async function setup(t:{after(fn:()=>Promise<void>):void}){
 const root=await mkdtemp(join(tmpdir(),'melife-local-friends-'));
 await cp('tests/fixtures/local-demo',root,{recursive:true});
 t.after(()=>rm(root,{recursive:true,force:true}));
 const repository=new FileLocalDemoRepository(root);
 const sessions=new LocalSessionStore();
 const now=Date.UTC(2026,10,17,10);
 return {root,repository,sessions,now,handler:createLocalFriendsHandler({repo:repository,sessions,environment:'development',enabled:true,now:()=>now})};
}

function userRequest(method:string,token:string,body?:unknown,origin='http://localhost:3002',path='/api/local-demo/friends'){
 return new Request(`http://localhost:3002${path}`,{method,headers:{
  host:'localhost:3002',origin,'content-type':'application/json',[`cookie`]:`${LOCAL_SESSION_COOKIE}=${token}`,
 },...(body===undefined?{}:{body:JSON.stringify(body)})});
}

test('mock friend requests persist across user sessions and can be accepted and removed',async t=>{
 const {repository,sessions,handler,now,root}=await setup(t);
 const sora=sessions.issue('photography',now),aoi=sessions.issue('food-photo',now);
 assert.equal((await handler(userRequest('GET',sora,undefined,''))).status,200);
 const sent=await handler(userRequest('POST',sora,{targetId:'food-photo'}));
 assert.equal(sent.status,200);
 assert.equal((await handler(userRequest('POST',sora,{targetId:'food-photo'}))).status,409);

 const incoming=await (await handler(userRequest('GET',aoi,undefined,'http://localhost:3002','/api/local-demo/friends?nickname=%E3%81%9D%E3%82%89%EF%BC%88%E6%9E%B6%E7%A9%BA%EF%BC%89'))).json();
 assert.equal(incoming.incoming[0].id,'photography');
 assert.equal(incoming.matches[0].connectionStatus,'incoming');
 assert.equal((await handler(userRequest('PATCH',aoi,{targetId:'photography',action:'accept'}))).status,200);
 assert.deepEqual((await repository.readFriendPage('photography')).friends,[{id:'food-photo',nickname:'あおい（架空）'}]);
 assert.deepEqual((await repository.readFriendPage('food-photo')).friends,[{id:'photography',nickname:'そら（架空）'}]);
 assert.equal((await handler(userRequest('DELETE',sora,{targetId:'food-photo'}))).status,200);
 assert.equal((await repository.readFriendPage('photography')).friends.length,0);
 const saved=JSON.parse(await readFile(join(root,'friends.json'),'utf8'));
 assert.deepEqual(saved.connections,[]);
});

test('mock friend requests can be declined or cancelled and declined requests may be retried',async t=>{
 const {repository,sessions,handler,now}=await setup(t);
 const sora=sessions.issue('photography',now),haru=sessions.issue('newcomer',now);
 assert.equal((await handler(userRequest('POST',sora,{targetId:'newcomer'}))).status,200);
 assert.equal((await handler(userRequest('DELETE',sora,{targetId:'newcomer'}))).status,200);
 assert.equal((await repository.readFriendPage('newcomer')).incoming.length,0);

 await handler(userRequest('POST',sora,{targetId:'newcomer'}));
 assert.equal((await handler(userRequest('PATCH',haru,{targetId:'photography',action:'decline'}))).status,200);
 assert.equal((await repository.readFriendPage('photography')).outgoing.length,0);
 assert.equal((await handler(userRequest('POST',sora,{targetId:'newcomer'}))).status,200);
 assert.equal((await repository.readFriendPage('newcomer')).incoming.length,1);
 assert.equal((await handler(userRequest('PATCH',sora,{targetId:'newcomer',action:'accept'}))).status,409);
});

test('mock friend API is local-only, authenticated, origin-checked, and validates input',async t=>{
 const {handler,sessions,now,repository}=await setup(t);
 const token=sessions.issue('photography',now);
 assert.equal((await handler(new Request('http://localhost:3002/api/local-demo/friends',{headers:{host:'localhost:3002'}}))).status,401);
 assert.equal((await handler(userRequest('POST',token,{targetId:'food-photo'},'https://attacker.invalid'))).status,403);
 assert.equal((await handler(userRequest('POST',token,{targetId:'photography'}))).status,400);
 assert.equal((await handler(userRequest('POST',token,{targetId:'not-a-user'}))).status,400);
 assert.equal((await handler(userRequest('GET',token,undefined,'http://localhost:3002','/api/local-demo/friends?nickname=%E3%81%82'))).status,400);
 assert.equal((await handler(userRequest('PATCH',token,{targetId:'food-photo',action:'accept'}))).status,409);
 assert.equal((await createLocalFriendsHandler({repo:repository,sessions,environment:'production',enabled:true})(userRequest('GET',token))).status,404);
 assert.equal((await createLocalFriendsHandler({repo:repository,sessions,environment:'development',enabled:false})(userRequest('GET',token))).status,404);
});

test('mock friend repository serializes concurrent writes and rejects corrupt connection data',async t=>{
 const {repository,root}=await setup(t);
 await Promise.all([
  repository.requestFriend('photography','food-photo',new Date(Date.UTC(2026,10,17,10))),
  repository.requestFriend('photography','newcomer',new Date(Date.UTC(2026,10,17,10))),
 ]);
 assert.equal((await repository.readFriendPage('photography')).outgoing.length,2);
 await rm(join(root,'friends.json'));
 await assert.rejects(repository.readFriendPage('photography'));
});
