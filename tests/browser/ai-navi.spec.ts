import { expect, test } from '@playwright/test';

test('sample-only demo explains missing API configuration',async({page})=>{
 await page.goto('/ai-navi');
 await expect(page.getByRole('heading',{name:/次の「やってみたい」を\s*見つけよう。/})).toBeVisible();
 await page.getByRole('button',{name:'AIに提案してもらう'}).click();
 await expect(page.getByRole('main').getByRole('alert')).toContainText('OPENAI_API_KEY');
});

test('shows AI suggestions as text and keeps sample context visible',async({page})=>{
 await page.route('**/api/ai-navi/demo',route=>route.fulfill({json:{suggestions:[{materialId:'flower-photo',reason:'<b>写真の学びから</b>',nextLearning:'窓辺の光で生花を撮る'}]}}));
 await page.goto('/ai-navi');
 await page.getByRole('button',{name:'AIに提案してもらう'}).click();
 await expect(page.getByTestId('suggestions')).toContainText('<b>写真の学びから</b>');
 await expect(page.getByTestId('suggestions').locator('b')).toHaveCount(0);
 await expect(page.getByTestId('suggestions')).toContainText('生花を美しく撮る');
 await expect(page.getByText('80%（4 / 5人）')).toBeVisible();
});

test('prevents double submission and discards a result after switching sample',async({page})=>{
 let release!:()=>void;
 const pending=new Promise<void>(resolve=>{release=resolve});
 await page.route('**/api/ai-navi/demo',async route=>{
  await pending;
  await route.fulfill({json:{suggestions:[{materialId:'flower-photo',reason:'古い提案',nextLearning:'次の学び'}]}}).catch(()=>{});
 });
 await page.goto('/ai-navi');
 await page.getByRole('button',{name:'AIに提案してもらう'}).click();
 await expect(page.getByRole('button',{name:'提案を考えています…'})).toBeDisabled();
 await page.getByLabel('サンプル利用者').selectOption('newcomer');
 release();
 await expect(page.getByTestId('suggestions')).toHaveCount(0);
 await expect(page.getByRole('button',{name:'AIに提案してもらう'})).toBeEnabled();
});
