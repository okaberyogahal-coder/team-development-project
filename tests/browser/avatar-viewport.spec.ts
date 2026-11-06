import {expect,test} from '@playwright/test';
import {avatarUser,mockPreview} from '../support/avatar-v3';
for(const viewport of [{width:1440,height:1000},{width:1440,height:800},{width:1024,height:600},{width:390,height:844},{width:844,height:390}]){
 test(`member wardrobe stays reachable at ${viewport.width}x${viewport.height}`,async({page})=>{
  await page.setViewportSize(viewport);await avatarUser(page);await mockPreview(page);await page.goto('/avatar');
  await expect(page.locator('.avatar-base-card').first()).toBeEnabled();await page.getByRole('button',{name:'このアバターで編集へ →'}).click();
  const complete=page.getByRole('button',{name:'このアバターで街へ行く'});await expect(complete).toBeEnabled();await complete.scrollIntoViewIfNeeded();await expect(complete).toBeInViewport();
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
 });
}
