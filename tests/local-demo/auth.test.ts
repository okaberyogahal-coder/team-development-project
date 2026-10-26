import assert from 'node:assert/strict';
import test from 'node:test';
import {cp,mkdtemp,rm,writeFile,symlink,unlink} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {createCredentialVerifier} from '../../server/local-demo/credentials';
import {LocalSessionStore,getLocalSessionStore,getSessionToken,sessionCookie,LOCAL_SESSION_COOKIE} from '../../server/local-demo/session';

async function setup(t:{after(fn:()=>Promise<void>):void}){
 const root=await mkdtemp(join(tmpdir(),'melife-auth-'));await cp('tests/fixtures/local-demo',root,{recursive:true});
 t.after(()=>rm(root,{recursive:true,force:true}));return root;
}
test('verifies only registered credentials, normalizing email but preserving password',async t=>{
 const verify=createCredentialVerifier(await setup(t));
 assert.equal(await verify(' SORA@EXAMPLE.TEST ','MilifeDemo2026!'),'photography');
 assert.equal(await verify('aoi@example.test','MilifeDemo2026!'),'food-photo');
 assert.equal(await verify('haru@example.test','MilifeDemo2026!'),'newcomer');
 assert.equal(await verify('sora@example.test','wrong'),null);
 assert.equal(await verify('unknown@example.test','MilifeDemo2026!'),null);
 assert.equal(await verify('sora@example.test',' MilifeDemo2026!'),null);
 assert.equal(await verify('x'.repeat(255),'x'),null);assert.equal(await verify('sora@example.test','x'.repeat(129)),null);
});
test('corrupt, missing and symlink credential stores fail closed',async t=>{
 const root=await setup(t);const verify=createCredentialVerifier(root);
 await writeFile(join(root,'credentials.json'),'{broken');await assert.rejects(verify('sora@example.test','x'));
 await writeFile(join(root,'credentials.json'),JSON.stringify({schemaVersion:1,accounts:[]}));await assert.rejects(verify('sora@example.test','x'));
 await unlink(join(root,'credentials.json'));await assert.rejects(verify('sora@example.test','x'));
 await symlink(join(process.cwd(),'tests/fixtures/local-demo/credentials.json'),join(root,'credentials.json'));await assert.rejects(verify('sora@example.test','x'));
});
test('random sessions expire, revoke, share process state, and limit retained tokens',()=>{
 const store=new LocalSessionStore();const now=100;
 const token=store.issue('photography',now);assert.match(token,/^[a-f0-9]{64}$/);
 assert.equal(store.resolve(token,now),'photography');assert.equal(store.resolve('photography',now),null);
 assert.equal(store.resolve(token,now+8*60*60*1000),null);
 const second=store.issue('newcomer',now);store.revoke(second);assert.equal(store.resolve(second,now),null);
 assert.equal(getLocalSessionStore(),getLocalSessionStore());
 const first=store.issue('photography',now);
 for(let i=0;i<1000;i++)store.issue('newcomer',now+i);
 assert.equal(store.resolve(first,now+1000),null);
});
test('session cookies are private and reject legacy user IDs, duplicates and malformed tokens',()=>{
 const token=new LocalSessionStore().issue('photography',0);
 const req=(cookie:string)=>new Request('https://localhost/api/local-demo',{headers:{cookie}});
 assert.equal(getSessionToken(req(`${LOCAL_SESSION_COOKIE}=${token}`)),token);
 assert.equal(getSessionToken(req(`melife.local-demo.user.v1=photography`)),null);
 assert.equal(getSessionToken(req(`${LOCAL_SESSION_COOKIE}=photography`)),null);
 assert.equal(getSessionToken(req(`${LOCAL_SESSION_COOKIE}=${token}; ${LOCAL_SESSION_COOKIE}=${token}`)),null);
 assert.match(sessionCookie(token,req('')),/HttpOnly; SameSite=Lax; Max-Age=28800; Secure/);
 assert.match(sessionCookie(null,req('')),/Max-Age=0/);
});
