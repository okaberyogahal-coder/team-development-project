import {test,expect} from '@playwright/test';
import {avatarUser,legacySave,mockPreview} from '../support/avatar-v3';
import {DEMO_PROFILE_KEY,DEFAULT_AVATAR} from '../../shared/demo-profile';
test('anonymous editor redirects to login',async({page})=>{await page.goto('/avatar');await expect(page).toHaveURL(/\/login$/);});
test('member UI exposes six models, owner draft and keeps completed legacy until confirmation',async({page})=>{
 const owner=await avatarUser(page);await legacySave(page,owner);await mockPreview(page);
 await page.goto('/avatar');await expect(page.locator('.avatar-base-card')).toHaveCount(6);
 for(const button of await page.locator('.avatar-base-card').all())await expect(button).toBeEnabled();
 await page.locator('.avatar-base-card').last().click();await page.getByRole('button',{name:'このアバターで編集へ →'}).click();
 await expect(page.getByRole('button',{name:'このアバターで街へ行く'})).toBeEnabled();
 expect(await page.evaluate(key=>JSON.parse(localStorage.getItem(key)!).appearance,DEMO_PROFILE_KEY)).toEqual(DEFAULT_AVATAR);
 await page.reload();await expect(page.getByRole('button',{name:'このアバターで街へ行く'})).toBeEnabled();
 expect(await page.evaluate(({owner})=>JSON.parse(localStorage.getItem('melife.avatar.v3.draft:'+owner)!).choice.baseAvatarId,{owner})).toBe('elderly-woman');
 await page.screenshot({path:test.info().outputPath('member-wardrobe.png')});
});
test('stale, foreign-source and rejected preview receipts cannot complete; changed session cannot save',async({page})=>{
 const owner=await avatarUser(page);await legacySave(page,owner);await mockPreview(page,false);await page.goto('/avatar');
 await expect(page.locator('.avatar-base-card').first()).toBeEnabled();await page.getByRole('button',{name:'このアバターで編集へ →'}).click();
 const submit=page.getByRole('button',{name:'このアバターで街へ行く'});await expect(submit).toBeDisabled();
 await page.evaluate(()=>postMessage({type:'miraifu:avatar-state',revision:0,state:{accepted:true,pairMatched:true}},location.origin));await expect(submit).toBeDisabled();
 const frame=page.frameLocator('iframe[title="3Dアバタープレビュー"]');
 await frame.locator('body').evaluate(()=>{const w=window as unknown as {received:{revision:number;choice:unknown}};parent.postMessage({type:'miraifu:avatar-state',revision:w.received.revision-1,state:{accepted:true,pairMatched:true,choice:w.received.choice}},location.origin);});await expect(submit).toBeDisabled();
 await frame.locator('body').evaluate(()=>{const w=window as unknown as {received:{revision:number;choice:unknown}};parent.postMessage({type:'miraifu:avatar-state',revision:w.received.revision,state:{accepted:true,pairMatched:true,choice:w.received.choice}},location.origin);});await expect(submit).toBeEnabled();
 await page.route('**/api/unity/session',route=>route.fulfill({json:{authenticated:true,profile:{userId:'another-owner'}}}));
 await submit.click();await expect(page.getByText('ログイン状態が変わりました。再度ログインしてください。')).toBeVisible();
 expect(await page.evaluate(key=>JSON.parse(localStorage.getItem(key)!).appearance,DEMO_PROFILE_KEY)).toEqual(DEFAULT_AVATAR);
});
test('missing preview never permits completion',async({page})=>{
 await avatarUser(page);await page.route('**/unity/AvatarV3Unified20261104/index.html*',route=>route.fulfill({status:404}));await page.goto('/avatar');
 await expect(page.getByText(/プレビューを確認できるまで確定できません/)).toBeVisible();await page.getByRole('button',{name:'このアバターで編集へ →'}).click();await expect(page.getByRole('button',{name:'このアバターで街へ行く'})).toBeDisabled();
});
test('blocked final storage preserves legacy outfit and remains in the member editor',async({page})=>{
 const owner=await avatarUser(page);await legacySave(page,owner);await mockPreview(page);await page.goto('/avatar');
 await expect(page.locator('.avatar-base-card').first()).toBeEnabled();await page.getByRole('button',{name:'このアバターで編集へ →'}).click();
 const submit=page.getByRole('button',{name:'このアバターで街へ行く'});await expect(submit).toBeEnabled();
 await page.evaluate(key=>{const original=Storage.prototype.setItem;Storage.prototype.setItem=function(k,v){if(k===key)throw new Error('保存できませんでした。');return original.call(this,k,v);};},DEMO_PROFILE_KEY);
 await submit.click();await expect(page.getByText('保存できませんでした。',{exact:true})).toBeVisible();await expect(page).toHaveURL(/\/avatar$/);
 expect(await page.evaluate(key=>JSON.parse(localStorage.getItem(key)!).appearance,DEMO_PROFILE_KEY)).toEqual(DEFAULT_AVATAR);
});
for(const viewport of [{width:1024,height:600},{width:390,height:844}])test(`member wardrobe controls remain usable at ${viewport.width}x${viewport.height}`,async({page})=>{
 await page.setViewportSize(viewport);await avatarUser(page);await mockPreview(page);await page.goto('/avatar');
 await expect(page.locator('.avatar-base-card').last()).toBeEnabled();await page.locator('.avatar-base-card').last().click();await page.getByRole('button',{name:'このアバターで編集へ →'}).click();
 const submit=page.getByRole('button',{name:'このアバターで街へ行く'});await expect(submit).toBeEnabled();await submit.scrollIntoViewIfNeeded();await expect(submit).toBeInViewport();
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);await page.screenshot({path:test.info().outputPath(`member-${viewport.width}.png`),fullPage:true});
});
for(const action of ['auth-change','unmount'] as const)test(`pending completion is invalidated by ${action}`,async({page})=>{
 const owner=await avatarUser(page);await legacySave(page,owner);await mockPreview(page);await page.goto('/avatar');await expect(page.locator('.avatar-base-card').first()).toBeEnabled();await page.getByRole('button',{name:'このアバターで編集へ →'}).click();
 const submit=page.getByRole('button',{name:'このアバターで街へ行く'});await expect(submit).toBeEnabled();
 let release!:()=>void;const held=new Promise<void>(resolve=>{release=resolve;});let requested!:()=>void;const started=new Promise<void>(resolve=>{requested=resolve;});
 await page.route('**/api/unity/session',async route=>{requested();await held;await route.fulfill({json:{authenticated:true,profile:{userId:owner}}});});
 await submit.click();await started;
 if(action==='auth-change'){
  await page.evaluate(key=>{localStorage.setItem(key,JSON.stringify({version:1,id:'another-owner',nickname:'B',appearance:null}));const channel=new BroadcastChannel('melife-auth');channel.postMessage('changed');channel.close();},DEMO_PROFILE_KEY);
  await page.waitForTimeout(100);
 }else{await page.getByRole('link',{name:'マイページ',exact:true}).click();await expect(page).toHaveURL(/\/account$/);}
 const response=page.waitForResponse('**/api/unity/session');release();await response;
 if(action==='auth-change'){
  await expect(page.getByText('ログイン状態が変わりました。再度ログインしてください。')).toBeVisible();expect(await page.evaluate(key=>JSON.parse(localStorage.getItem(key)!).id,DEMO_PROFILE_KEY)).toBe('another-owner');await expect(page).toHaveURL(/\/avatar$/);
 }else{await page.waitForTimeout(300);await expect(page).toHaveURL(/\/account$/);expect(await page.evaluate(key=>JSON.parse(localStorage.getItem(key)!).appearance,DEMO_PROFILE_KEY)).toEqual(DEFAULT_AVATAR);}
});
