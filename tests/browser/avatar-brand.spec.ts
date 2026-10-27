import {expect,test} from '@playwright/test';
import {DEFAULT_AVATAR,DEMO_PROFILE_KEY} from '../../shared/demo-profile';

for(const width of [1440,390])test(`avatar shell matches TOP branding at ${width}px`,async({page})=>{
 await page.addInitScript(({key,appearance})=>localStorage.setItem(key,JSON.stringify({version:1,id:'brand-test',nickname:'みらい',appearance})),{key:DEMO_PROFILE_KEY,appearance:DEFAULT_AVATAR});
 // This test covers the surrounding HTML only; separate tests run the actual WebGL build.
 await page.route('**/unity/FutureCity/index.html*',route=>route.fulfill({contentType:'text/html',body:'<!doctype html><title>Shell fixture</title>'}));
 await page.setViewportSize({width,height:900});await page.goto('/avatar');
 await expect(page.locator('.avatar-setup-page')).toBeVisible();
 await expect(page.getByRole('heading',{name:'あなたらしい姿で、はじめよう。'})).toHaveCSS('color','rgb(21, 59, 120)');
 await expect(page.locator('.home-brand-logo-image')).toHaveAttribute('src',/milife-logo-matte/);
 await expect(page.getByRole('link',{name:'TOPへ戻る',exact:true})).toBeVisible();
 await expect(page.locator('.avatar-setup-onboarding')).toContainText('みらい');
 await expect(page.getByTitle('3Dアバター作成')).toBeVisible();
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
 await page.screenshot({path:`/private/tmp/avatar-shell-${width}.png`,fullPage:true});
 await page.goto('/plaza');await expect(page.locator('.avatar-setup-page')).toHaveCount(0);
});
