import {expect,test} from '@playwright/test';
import {DEMO_PROFILE_KEY} from '../../shared/demo-profile';

test('a sign-in response without a valid server session cannot open the avatar editor',async({page})=>{
 // Mock only the successful sign-in response: never create accounts on the shared DB.
 await page.addInitScript(key=>localStorage.setItem(key,JSON.stringify({version:1,id:'login-navigation-fixture',nickname:'確認用',appearance:null})),DEMO_PROFILE_KEY);
 await page.route('**/api/auth/sign-in/email',route=>route.fulfill({json:{success:true}}));
 await page.route('**/unity/FutureCity/index.html*',route=>route.fulfill({contentType:'text/html',body:'<!doctype html><title>Unity fixture</title>'}));
 await page.goto('/login');
 await page.getByLabel('メールアドレス',{exact:true}).fill('navigation@example.invalid');
 await page.getByLabel('パスワード',{exact:true}).fill('fixture-password-12345');
 const signedIn=page.waitForResponse('**/api/auth/sign-in/email');
 await page.getByRole('button',{name:'ログイン',exact:true}).click();await signedIn;
 const guarded=await page.request.get('/avatar',{maxRedirects:0});expect(guarded.status()).toBe(307);expect(guarded.headers().location).toBe('/login');
 await expect(page).toHaveURL(/\/login$/);
 await expect(page.getByTitle('3Dアバタープレビュー')).toHaveCount(0);
});
