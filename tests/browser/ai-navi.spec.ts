import { expect, test } from '@playwright/test';
const theme={theme:'季節の花を撮る',reason:'<b>写真の受講と花の閲覧のつながりから</b>',firstStep:'窓辺の花を一枚撮ってみる',basis:'history',evidenceIds:['complete:natural-light','view:flower-arrange'],relatedMaterialIds:[]};

test('uses observed learning history instead of asking the user to describe interests',async({page})=>{
 await page.goto('/ai-navi');
 await expect(page.getByRole('heading',{name:/履歴から見つかる、\s*新しい楽しみ。/})).toBeVisible();
 await expect(page.getByTestId('learning-history')).toContainText('受講完了');
 await expect(page.getByTestId('learning-history')).toContainText('閲覧');
 await expect(page.locator('input, textarea')).toHaveCount(0);
 await page.getByRole('button',{name:'履歴からテーマを見つける'}).click();
 await expect(page.getByRole('main').getByRole('alert')).toContainText('OPENAI_API_KEY');
});

test('shows a hobby as the main proposal, with observed evidence and no required material',async({page})=>{
 await page.route('**/api/ai-navi/demo',route=>route.fulfill({json:{suggestions:[theme]}}));
 await page.goto('/ai-navi');
 await page.getByRole('button',{name:'履歴からテーマを見つける'}).click();
 const result=page.getByTestId('suggestions');
 await expect(result.getByRole('heading',{name:'季節の花を撮る'})).toBeVisible();
 await expect(result).toContainText('<b>写真の受講と花の閲覧のつながりから</b>');
 await expect(result.locator('b')).toHaveCount(0);
 await expect(result).toContainText('窓辺の花を一枚撮ってみる');
 await expect(result).toContainText('自然光と背景ぼけの基本（受講完了）');
 await expect(result).toContainText('季節の花を楽しむ、はじめての花飾り（閲覧）');
 await expect(result.getByText('関連する教材')).toHaveCount(0);
 await page.screenshot({path:'/private/tmp/ai-navi-discovery-desktop.png',fullPage:true});
 await page.setViewportSize({width:390,height:844});
 expect(await page.evaluate(()=>document.documentElement.scrollWidth>window.innerWidth)).toBe(false);
 await page.screenshot({path:'/private/tmp/ai-navi-discovery-mobile.png',fullPage:true});
});

test('no-history suggestions explain measured popularity, with optional supporting material',async({page})=>{
 await page.route('**/api/ai-navi/demo',route=>route.fulfill({json:{suggestions:[{...theme,basis:'popularity',evidenceIds:['flower-photo'],relatedMaterialIds:['flower-photo']}]}}));
 await page.goto('/ai-navi');
 await page.getByLabel('テスト用の履歴ケース').selectOption('newcomer');
 await expect(page.getByTestId('learning-history')).toContainText('まだ履歴がありません');
 await page.getByRole('button',{name:'履歴からテーマを見つける'}).click();
 await expect(page.getByTestId('suggestions')).toContainText('人気を入口にした提案');
 await expect(page.getByTestId('suggestions')).toContainText('80%（4 / 5人）');
 await page.getByTestId('suggestions').getByText('関連する教材').click();
 await expect(page.getByTestId('suggestions')).toContainText('生花を美しく撮る、小さな写真教室');
});

test('prevents double submission and discards a result after switching history cases',async({page})=>{
 let release!:()=>void;
 const pending=new Promise<void>(resolve=>{release=resolve});
 await page.route('**/api/ai-navi/demo',async route=>{
  await pending;
  await route.fulfill({json:{suggestions:[theme]}}).catch(()=>{});
 });
 await page.goto('/ai-navi');
 await page.getByRole('button',{name:'履歴からテーマを見つける'}).click();
 await expect(page.getByRole('button',{name:'テーマを考えています…'})).toBeDisabled();
 await page.getByLabel('テスト用の履歴ケース').selectOption('food-photo');
 release();
 await expect(page.getByTestId('suggestions')).toHaveCount(0);
 await expect(page.getByTestId('learning-history')).toContainText('毎日に役立つ、やさしい料理');
 await expect(page.getByRole('button',{name:'履歴からテーマを見つける'})).toBeEnabled();
});
