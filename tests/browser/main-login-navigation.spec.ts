import {expect,test} from '@playwright/test';
import {DEMO_PROFILE_KEY} from '../../shared/demo-profile';

test('database login success proceeds to the avatar setup screen',async({page})=>{
 // Mock only the successful sign-in response: never create accounts on the shared DB.
 await page.addInitScript(key=>localStorage.setItem(key,JSON.stringify({version:1,id:'login-navigation-fixture',nickname:'確認用',appearance:null})),DEMO_PROFILE_KEY);
 await page.route('**/api/auth/sign-in/email',route=>route.fulfill({json:{success:true}}));
 await page.route('**/unity/FutureCity/index.html*',route=>route.fulfill({contentType:'text/html',body:'<!doctype html><title>Unity fixture</title>'}));
 await page.goto('/login');
 await page.getByLabel('メールアドレス',{exact:true}).fill('navigation@example.invalid');
 await page.getByLabel('パスワード',{exact:true}).fill('fixture-password-12345');
 await page.getByRole('button',{name:'ログイン',exact:true}).click();
 await expect(page).toHaveURL(/\/avatar$/);
 await expect(page.getByTitle('3Dアバター作成')).toBeVisible();
});
