import {expect,test} from '@playwright/test';
import {resetSharedFixture,selectUser} from './local-demo-fixture';
const theme={theme:'季節の花を撮る',reason:'<b>写真の受講と花の閲覧のつながりから</b>',firstStep:'窓辺の花を一枚撮ってみる',basis:'history',evidenceIds:['complete:natural-light','view:flower-arrange'],relatedMaterialIds:[]};
test.beforeEach(async({page,baseURL})=>{await resetSharedFixture(page);await selectUser(page,'photography',baseURL!);});

test('uses saved history instead of asking the user to describe interests',async({page})=>{
 await page.goto('/ai-navi');
 await expect(page.getByRole('heading',{name:/履歴から見つかる、\s*新しい楽しみ。/})).toBeVisible();
 await expect(page.getByTestId('learning-history')).toContainText('受講完了');
 await expect(page.getByTestId('learning-history')).toContainText('閲覧');
 await expect(page.locator('input, textarea')).toHaveCount(0);
 await page.getByRole('button',{name:'履歴からテーマを見つける'}).click();
 await expect(page.getByRole('main').getByRole('alert')).toContainText('OPENAI_API_KEY');
});

test('shows a hobby and recorded evidence with no required related material',async({page})=>{
 await page.route('**/api/ai-navi/demo',async route=>{
  const state=await (await page.request.get('/api/local-demo')).json();
  await route.fulfill({json:{suggestions:[theme],revision:state.revision}});
 });
 await page.goto('/ai-navi');await page.getByRole('button',{name:'履歴からテーマを見つける'}).click();
 const result=page.getByTestId('suggestions');
 await expect(result.getByRole('heading',{name:'季節の花を撮る'})).toBeVisible();
 await expect(result).toContainText('<b>写真の受講と花の閲覧のつながりから</b>');await expect(result.locator('b')).toHaveCount(0);
 await expect(result).toContainText('自然光と背景ぼけの基本（受講完了）');
 await expect(result.getByText('関連する教材')).toHaveCount(0);
 await page.screenshot({path:'/private/tmp/ai-navi-local-desktop.png',fullPage:true});
 await page.setViewportSize({width:390,height:844});expect(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth)).toBe(false);
 await page.screenshot({path:'/private/tmp/ai-navi-local-mobile.png',fullPage:true});
});

test('newcomer recommendations cite popularity computed from the shared files',async({page,baseURL})=>{
 await selectUser(page,'newcomer',baseURL!);
 await page.route('**/api/ai-navi/demo',async route=>{
  const state=await (await page.request.get('/api/local-demo')).json();
  await route.fulfill({json:{suggestions:[{...theme,basis:'popularity',evidenceIds:['natural-light'],relatedMaterialIds:['natural-light']}],revision:state.revision}});
 });
 await page.goto('/ai-navi');await expect(page.getByTestId('learning-history')).toContainText('まだ履歴がありません');
 await page.getByRole('button',{name:'履歴からテーマを見つける'}).click();
 await expect(page.getByTestId('suggestions')).toContainText('人気を入口にした提案');
 await expect(page.getByTestId('suggestions')).toContainText('100%（2 / 2人）');
});

test('switching user while AI works discards the old response',async({page})=>{
 let release!:()=>void;const pending=new Promise<void>(resolve=>{release=resolve});
 await page.route('**/api/ai-navi/demo',async route=>{await pending;await route.fulfill({json:{suggestions:[theme],revision:'old'}}).catch(()=>{});});
 await page.goto('/ai-navi');await page.getByRole('button',{name:'履歴からテーマを見つける'}).click();
 await expect(page.getByRole('button',{name:'テーマを考えています…'})).toBeDisabled();
 await page.getByLabel('開発用ユーザー切替').selectOption('food-photo');release();
 await expect(page.getByTestId('learning-history')).toContainText('毎日に役立つ、やさしい料理');
 await expect(page.getByTestId('suggestions')).toHaveCount(0);
});

test('a history update while AI works discards the old response',async({page})=>{
 let release!:()=>void;const pending=new Promise<void>(resolve=>{release=resolve});
 await page.route('**/api/ai-navi/demo',async route=>{await pending;await route.fulfill({json:{suggestions:[theme],revision:'old'}}).catch(()=>{});});
 await page.goto('/ai-navi');await page.getByRole('button',{name:'履歴からテーマを見つける'}).click();
 await page.getByText('サンプル教材を開いて履歴を記録').click();await page.getByRole('button',{name:'スマートフォンで写真を整理するを開く'}).click();release();
 await expect(page.getByTestId('learning-history')).toContainText('スマートフォンで写真を整理する');await expect(page.getByTestId('suggestions')).toHaveCount(0);
});


test('a selection changed outside the current view is rechecked before displaying AI results',async({page,baseURL})=>{
 let release!:()=>void;const pending=new Promise<void>(resolve=>{release=resolve});
 await page.route('**/api/ai-navi/demo',async route=>{
  const state=await (await page.request.get('/api/local-demo')).json();await pending;
  await route.fulfill({json:{suggestions:[theme],revision:state.revision}}).catch(()=>{});
 });
 await page.goto('/ai-navi');await page.getByRole('button',{name:'履歴からテーマを見つける'}).click();
 await expect(page.getByRole('button',{name:'テーマを考えています…'})).toBeDisabled();
 await selectUser(page,'food-photo',baseURL!);release();
 await expect(page.getByTestId('local-current-user')).toContainText('あおい（架空）');
 await expect(page.getByTestId('suggestions')).toHaveCount(0);
});

test('logging out during AI generation never leaves the old result visible',async({page})=>{
 let release!:()=>void;const pending=new Promise<void>(resolve=>{release=resolve});
 await page.route('**/api/ai-navi/demo',async route=>{await pending;await route.fulfill({json:{suggestions:[theme],revision:'old'}}).catch(()=>{});});
 await page.goto('/ai-navi');await page.getByRole('button',{name:'履歴からテーマを見つける'}).click();
 await page.getByRole('button',{name:'開発用ログアウト'}).click();release();
 await expect(page.getByRole('button',{name:'そら（架空）で開発用ログイン'})).toBeVisible();
 await expect(page.getByTestId('suggestions')).toHaveCount(0);
});

test('a delayed AI confirmation GET cannot restore a logged-out user',async({page})=>{
 await page.goto('/ai-navi');
 await expect(page.getByTestId('local-current-user')).toBeVisible();
 const snapshot=await (await page.request.get('/api/local-demo')).json();
 let release!:()=>void;const pending=new Promise<void>(resolve=>{release=resolve});
 let started!:()=>void;const requested=new Promise<void>(resolve=>{started=resolve});
 await page.route('**/api/local-demo',async route=>{
  if(route.request().method()!=='GET')return route.continue();
  started();await pending;await route.fulfill({json:snapshot}).catch(()=>{});
 });
 await page.route('**/api/ai-navi/demo',route=>route.fulfill({json:{suggestions:[theme],revision:snapshot.revision}}));
 await page.getByRole('button',{name:'履歴からテーマを見つける'}).click();await requested;
 await page.getByRole('button',{name:'開発用ログアウト'}).click();
 await expect(page.getByTestId('local-current-user')).toHaveCount(0);
 release();await page.waitForTimeout(300);
 await expect(page.getByTestId('local-current-user')).toHaveCount(0);
});

test('an old tab cannot save history into a newly selected user',async({page,baseURL})=>{
 await page.goto('/ai-navi');await expect(page.getByTestId('local-current-user')).toContainText('そら');
 await selectUser(page,'newcomer',baseURL!);
 await page.getByText('サンプル教材を開いて履歴を記録').click();
 await page.getByRole('button',{name:'スマートフォンで写真を整理するを開く'}).click();
 await expect(page.getByTestId('local-current-user')).toContainText('はる');
 const state=await (await page.request.get('/api/local-demo')).json();
 expect(state.currentUser.events).toEqual([]);
});
