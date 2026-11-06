import {expect,test} from '@playwright/test';
import {avatarUser,mockPreview} from '../support/avatar-v3';
for(const width of [1440,390])test(`member wardrobe retains TOP logo and theme at ${width}px`,async({page})=>{
 await page.setViewportSize({width,height:900});await avatarUser(page);await mockPreview(page);await page.goto('/avatar');
 await expect(page.locator('.avatar-page')).toBeVisible();await expect(page.getByRole('heading',{name:'アバター編集',exact:true})).toHaveCSS('color','rgb(21, 59, 120)');
 await expect(page.locator('.home-brand-logo-image')).toHaveAttribute('src',/milife-logo-matte/);await expect(page.getByRole('link',{name:'TOPへ戻る',exact:true})).toBeVisible();
 await expect(page.getByTitle('3Dアバタープレビュー')).toBeVisible();await expect(page.locator('.avatar-base-card')).toHaveCount(6);
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
});
// Real six-model rendering, wardrobe application and entry are covered by avatar-v3-flow.spec.ts.
