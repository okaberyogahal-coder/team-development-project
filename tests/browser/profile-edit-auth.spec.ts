import {test,expect,type Page} from '@playwright/test';
import {randomUUID} from 'node:crypto';
import {existsSync} from 'node:fs';
import {createDatabase} from '../../server/db/connection';
import {authTestDatabaseUrl} from '../support/auth-database';
const db=createDatabase(authTestDatabaseUrl());const emails:string[]=[];const origin='http://127.0.0.1:3012';
const password='profile-edit-test-password-123';
test.afterAll(async()=>{try{await db.user.deleteMany({where:{email:{in:emails}}});}finally{await db.$disconnect();}});
async function register(page:Page,nickname='写真のひと'){
 const email=`profile-${randomUUID()}@example.invalid`;emails.push(email);
 const response=await page.request.post('/api/account/signup',{headers:{Origin:origin},data:{email,password,confirmation:password,nickname,avatar:'cat',adult:true,terms:true,privacy:true,interests:['写真'],teaches:[],wantsToLearn:[],strengths:'',goal:''}});
 expect(response.ok()).toBe(true);expect((await response.json()).signedIn).toBe(true);
 return await db.user.findUniqueOrThrow({where:{email}});
}
async function open(page:Page){await page.goto('/account');await page.getByRole('link',{name:'プロフィールを編集',exact:true}).click();await expect(page.getByLabel('ニックネーム（必須）')).toHaveValue('写真のひと');}
test('edits survive reload and only the owner can save',async({page,browser})=>{
 const user=await register(page);await open(page);
 await page.getByLabel('ニックネーム（必須）').fill('花と写真');
 await page.getByLabel('興味のある分野',{exact:true}).fill('写真');
 await page.getByRole('button',{name:'興味のある分野を追加',exact:true}).click();
 await page.getByLabel('興味のある分野 2',{exact:true}).fill('園芸');
 await page.getByLabel('学びたいこと',{exact:true}).fill('花の育て方');
 await page.getByLabel('教えられること',{exact:true}).fill('カメラ');
 await page.getByLabel('得意なこと（本人のみ）').fill('人の話を聞く');
 await page.getByLabel('目標（本人のみ）').fill('毎月写真を撮る');
 await page.getByRole('button',{name:'変更を保存',exact:true}).click();
 await expect(page).toHaveURL(/\/account\?updated=1$/);
 await expect(page.getByRole('status')).toContainText('プロフィールを保存しました');
 await page.reload();await expect(page.getByRole('heading',{name:'こんにちは、花と写真さん。'})).toBeVisible();
 const row=await db.user.findUniqueOrThrow({where:{id:user.id},include:{profile:true,preferences:true}});
 expect(row.profile?.nickname).toBe('花と写真');expect(row.profile?.avatarId).toBe('cat');expect(row.preferences?.goal).toBe('毎月写真を撮る');
 const unity=await (await page.request.get('/api/unity/session')).json();expect(unity.profile.nickname).toBe('花と写真');expect(unity.profile.teaches).toEqual(['カメラ']);expect(JSON.stringify(unity)).not.toContain('毎月写真を撮る');
 const second=await browser.newContext({baseURL:origin});
 try{
  expect((await second.request.get('/api/account/profile')).status()).toBe(401);
  const secondPage=await second.newPage();const other=await register(secondPage,'別のひと');
  const input=await (await page.request.get('/api/account/profile')).json();const {ownerId,...fields}=input;
  expect((await second.request.patch('/api/account/profile',{headers:{Origin:origin,'X-Profile-Owner':ownerId},data:fields})).status()).toBe(409);
  expect((await page.request.patch('/api/account/profile',{headers:{Origin:origin,'X-Profile-Owner':ownerId},data:{...fields,userId:other.id}})).status()).toBe(400);
  expect((await db.userProfile.findUniqueOrThrow({where:{userId:other.id}})).nickname).toBe('別のひと');
 }finally{await second.close();}
});
test('conflicting edits retain the draft and can reload the latest profile',async({page})=>{
 await register(page);await open(page);
 const {ownerId,...fields}=await (await page.request.get('/api/account/profile')).json();
 expect((await page.request.patch('/api/account/profile',{headers:{Origin:origin,'X-Profile-Owner':ownerId},data:{...fields,nickname:'別タブの変更'}})).ok()).toBe(true);
 await page.getByLabel('ニックネーム（必須）').fill('保存前の入力');await page.getByRole('button',{name:'変更を保存',exact:true}).click();
 await expect(page.locator('p[role=alert]')).toContainText('別の画面で更新');await expect(page.getByLabel('ニックネーム（必須）')).toHaveValue('保存前の入力');
 await expect(page.getByRole('button',{name:'変更を保存',exact:true})).toBeDisabled();
 await page.getByRole('button',{name:'入力内容を破棄して最新情報を読み込む'}).click();await expect(page.getByLabel('ニックネーム（必須）')).toHaveValue('別タブの変更');
});
test('mobile layout and a failed save keep inputs for retry',async({page})=>{
 await page.setViewportSize({width:390,height:844});await register(page);await open(page);
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
 await page.getByLabel('目標（本人のみ）').fill('失敗しても残す');
 await page.route('**/api/account/profile',async route=>{if(route.request().method()==='PATCH')await route.abort();else await route.continue();});
 await page.getByRole('button',{name:'変更を保存',exact:true}).click();await expect(page.locator('p[role=alert]')).toContainText('入力内容は残しています');await expect(page.getByLabel('目標（本人のみ）')).toHaveValue('失敗しても残す');
 await page.screenshot({path:'/private/tmp/melife-profile-edit-mobile.png',fullPage:true});
 await page.unroute('**/api/account/profile');await page.getByRole('button',{name:'変更を保存',exact:true}).click();await expect(page).toHaveURL(/\/account\?updated=1$/);
});
test('a changed login user cannot receive the previous user draft',async({page})=>{
 await register(page);await open(page);await page.getByLabel('ニックネーム（必須）').fill('前の利用者の入力');
 expect((await page.request.post('/api/auth/sign-out',{headers:{Origin:origin},data:{}})).ok()).toBe(true);
 const other=await register(page,'切替後の利用者');await page.getByRole('button',{name:'変更を保存',exact:true}).click();
 await expect(page.locator('p[role=alert]')).toContainText('ログイン中の利用者が変わりました');await expect(page.getByLabel('ニックネーム（必須）')).toHaveValue('前の利用者の入力');
 expect((await db.userProfile.findUniqueOrThrow({where:{userId:other.id}})).nickname).toBe('切替後の利用者');
});

test('a nickname-only edit preserves punctuation inside existing topics',async({page})=>{
 const user=await register(page);await db.learningPreferences.update({where:{userId:user.id},data:{interests:['料理、栄養','写真,映像','花\n自然']}});
 await open(page);await expect(page.getByLabel('興味のある分野',{exact:true})).toHaveValue('料理、栄養');
 await page.getByLabel('ニックネーム（必須）').fill('名前だけ変更');await page.getByRole('button',{name:'変更を保存',exact:true}).click();
 await expect(page).toHaveURL(/\/account\?updated=1$/);
 expect((await db.learningPreferences.findUniqueOrThrow({where:{userId:user.id}})).interests).toEqual(['料理、栄養','写真,映像','花\n自然']);
});

test('saved profile changes reach an already running Unity world',async({page,context})=>{
 test.skip(!existsSync('public/unity/FutureCity/index.html'),'Build Unity locally to verify live profile reflection.');
 test.setTimeout(180000);
 await register(page);const worldPage=await context.newPage();
 try{
  await worldPage.goto('/plaza');const world=worldPage.frameLocator('iframe[title="ミライフ Unityワールド"]');
  await expect(world.locator('body')).toHaveAttribute('data-session-state','authenticated',{timeout:150000});
  await expect(world.locator('#session-status')).toContainText('写真のひと');
  await open(page);await page.getByLabel('ニックネーム（必須）').fill('ワールドへ反映');await page.getByLabel('教えられること',{exact:true}).fill('写真撮影');
  await page.screenshot({path:'/private/tmp/melife-profile-edit-desktop.png',fullPage:true});
  await page.getByRole('button',{name:'変更を保存',exact:true}).click();await expect(page).toHaveURL(/\/account\?updated=1$/);
  await expect(world.locator('#session-status')).toContainText('ワールドへ反映');
  const applied=await world.locator('body').evaluate(()=>(window as Window&{melifeAppliedSession?:{nickname:string;teaches:string[]}}).melifeAppliedSession);
  expect(applied?.nickname).toBe('ワールドへ反映');expect(applied?.teaches).toEqual(['写真撮影']);
 }finally{await worldPage.close();}
});
