import {expect,test,type Page} from '@playwright/test';
import {randomUUID} from 'node:crypto';
import {authTestDatabaseUrl} from './auth-database';
import {BASE_AVATARS,DEFAULT_AVATAR_CHOICE} from '../../shared/avatar-types';
import {DEFAULT_AVATAR,DEMO_PROFILE_KEY} from '../../shared/demo-profile';
export async function avatarUser(page:Page){
 authTestDatabaseUrl();
 const email=`avatar-v3-${randomUUID()}@example.invalid`,password='avatar-v3-test-password-123';
 const result=await page.request.post('/api/account/signup',{headers:{Origin:new URL(test.info().project.use.baseURL!).origin},data:{email,password,confirmation:password,nickname:'着せ替え確認',avatar:'cat',gender:'male',age:30,adult:true,terms:true,privacy:true,interests:['写真'],wantsToLearn:[],teaches:[],strengths:'',goal:''}});
 expect(result.ok(),await result.text()).toBe(true);
 const session=await (await page.request.get('/api/unity/session')).json();
 return session.profile.userId as string;
}
export async function legacySave(page:Page,ownerId:string){
 await page.addInitScript(({ownerId,key,appearance})=>localStorage.setItem(key,JSON.stringify({version:1,id:ownerId,nickname:'以前の外見',appearance})),{ownerId,key:DEMO_PROFILE_KEY,appearance:DEFAULT_AVATAR});
}
const catalog={models:BASE_AVATARS.map(base=>({id:base.id,defaults:{...DEFAULT_AVATAR_CHOICE,baseAvatarId:base.id}})),slots:Object.entries({body:1,face:3,hair:3,outerwear:3,pants:3,shoes:3,glasses:2,accessory:5,costume:2}).map(([key,count])=>({key,count})),skinColorSupported:false};
export async function mockPreview(page:Page,autoAck=true){
 await page.route('**/unity/AvatarV3Unified20261104/index.html*',route=>route.fulfill({contentType:'text/html',body:`<!doctype html><body><script>
 window.autoAck=${autoAck};window.catalog=${JSON.stringify(catalog)};
 addEventListener('message',e=>{if(e.data?.type==='miraifu:catalog-request')parent.postMessage({type:'miraifu:avatar-catalog',catalog},location.origin);if(e.data?.type==='miraifu:avatar'){window.received=e.data;if(window.autoAck)parent.postMessage({type:'miraifu:avatar-state',revision:e.data.revision,state:{accepted:true,pairMatched:true,choice:e.data.choice}},location.origin);}});
 parent.postMessage({type:'miraifu:avatar-catalog',catalog},location.origin);
 parent.postMessage({type:'miraifu:avatar-ready'},location.origin);
 </script>`}));
}
