import {test,expect} from '@playwright/test';
import {avatarUser,mockPreview} from '../support/avatar-v3';
import {DEFAULT_AVATAR_CHOICE} from '../../shared/avatar-types';
import {DEMO_PROFILE_KEY} from '../../shared/demo-profile';

async function edit(page:import('@playwright/test').Page){
 await mockPreview(page);await page.goto('/avatar');
 await expect(page.locator('.avatar-base-card').last()).toBeEnabled();
 await page.locator('.avatar-base-card').last().click();
 await page.getByRole('button',{name:'このアバターで編集へ →'}).click();
 await page.getByRole('button',{name:'シック',exact:true}).click();
 const button=page.getByRole('button',{name:'このアバターで街へ行く'});await expect(button).toBeEnabled();return button;
}

test('main wardrobe saves to the account and restores in a browser with no local data',async({page,browser})=>{
 const owner=await avatarUser(page);const button=await edit(page);
 await page.route('**/unity/FutureCity/index.html',route=>route.fulfill({contentType:'text/html',body:'<!doctype html><title>World host</title>'}));
 await button.click();await expect(page).toHaveURL(/\/plaza$/);
 const saved=await (await page.request.get('/api/account/avatar')).json();
 expect(saved.ownerId).toBe(owner);expect(saved.version).toBe(1);expect(saved.appearance.choice.baseAvatarId).toBe('elderly-woman');
 const clean=await browser.newContext({storageState:{cookies:await page.context().cookies(),origins:[]}});
 try{
  const other=await clean.newPage();await mockPreview(other);await other.goto('/avatar');
  await expect(other.getByRole('button',{name:'このアバターで街へ行く'})).toBeEnabled();
  const preview=other.frameLocator('iframe[title="3Dアバタープレビュー"]');
  await expect.poll(()=>preview.locator('body').evaluate(()=>(window as unknown as {received:{choice:unknown}}).received?.choice)).toEqual(saved.appearance.choice);
  await other.goto('/account');await expect(other.getByText('外見はアカウントに保存されています。')).toBeVisible();
  await expect(other.getByRole('link',{name:'フレンドを探す'})).toHaveAttribute('href','/friends');
 }finally{await clean.close();}
});

test('a failed DB save keeps the current editor and its draft',async({page})=>{
 const owner=await avatarUser(page);const button=await edit(page);
 await page.route('**/api/account/avatar',route=>route.request().method()==='PUT'?route.fulfill({status:503,json:{message:'保存できませんでした。編集内容は残っています。'}}):route.continue());
 await button.click();await expect(page.getByText('保存できませんでした。編集内容は残っています。',{exact:true})).toBeVisible();
 await expect(page).toHaveURL(/\/avatar$/);
 expect((await (await page.request.get('/api/account/avatar')).json()).version).toBe(0);
 expect(await page.evaluate(owner=>JSON.parse(localStorage.getItem('melife.avatar.v3.draft:'+owner)!).choice.baseAvatarId,owner)).toBe('elderly-woman');
});

test('an existing browser outfit carries forward until the account has a newer saved appearance',async({page})=>{
 const owner=await avatarUser(page);
 const oldAppearance={version:3,assetProfile:'v3-unified-20261104',choice:{...DEFAULT_AVATAR_CHOICE,baseAvatarId:'young-woman'}};
 await page.addInitScript(({key,owner,appearance})=>{
  localStorage.setItem(key,JSON.stringify({version:1,id:owner,nickname:'以前の外見',appearance}));
 },{key:DEMO_PROFILE_KEY,owner,appearance:oldAppearance});
 await mockPreview(page);
 await page.goto('/avatar');
 const preview=page.frameLocator('iframe[title="3Dアバタープレビュー"]');
 const shownChoice=()=>preview.locator('body').evaluate(()=>(window as unknown as {received:{choice:unknown}}).received?.choice);
 await expect.poll(shownChoice).toEqual(oldAppearance.choice);
 const original=await (await page.request.get('/api/account/avatar')).json();
 expect(original).toEqual({ownerId:owner,appearance:null,version:0});

 const appearance={...oldAppearance,choice:{...oldAppearance.choice,baseAvatarId:'elderly-woman'}};
 const saved=await page.request.put('/api/account/avatar',{headers:{Origin:new URL(page.url()).origin,'X-Profile-Owner':owner},data:{appearance,version:0}});
 expect(saved.ok(),await saved.text()).toBe(true);
 await page.reload();
 await expect.poll(shownChoice).toEqual(appearance.choice);
});

test('stale saves receive 409 and cannot overwrite the saved account appearance',async({page})=>{
 const owner=await avatarUser(page);const first={version:3,assetProfile:'v3-unified-20261104',choice:{...DEFAULT_AVATAR_CHOICE,baseAvatarId:'young-woman'}};
 const options={headers:{Origin:new URL(test.info().project.use.baseURL!).origin,'X-Profile-Owner':owner},data:{appearance:first,version:0}};
 const responses=await Promise.all([page.request.put('/api/account/avatar',options),page.request.put('/api/account/avatar',{...options,data:{...options.data,appearance:{...first,choice:{...first.choice,topColor:'#FFFFFF'}}}})]);
 expect(responses.map(r=>r.status()).sort()).toEqual([200,409]);
 const winner=await responses.find(r=>r.status()===200)!.json();
 expect(await (await page.request.get('/api/account/avatar')).json()).toEqual(winner);
});
