import {expect,test} from '@playwright/test';
import {DEFAULT_AVATAR,DEMO_PROFILE_KEY} from '../../shared/demo-profile';
for(const viewport of [{width:1440,height:1000},{width:1440,height:800},{width:1024,height:600},{width:390,height:844},{width:844,height:390}]){
 test(`avatar page stays within the viewport at ${viewport.width}x${viewport.height}`,async({page})=>{
  await page.setViewportSize(viewport);
  await page.addInitScript(({key,appearance})=>localStorage.setItem(key,JSON.stringify({version:1,id:'viewport-test',nickname:'みらい',appearance})),{key:DEMO_PROFILE_KEY,appearance:DEFAULT_AVATAR});
  await page.route('**/unity/FutureCity/index.html*',route=>route.fulfill({contentType:'text/html',body:'<!doctype html><title>Viewport fixture</title>'}));
  await page.goto('/avatar');await expect(page.locator('.avatar-setup-onboarding')).toContainText('みらい');
  expect(await page.evaluate(()=>document.documentElement.scrollHeight<=innerHeight&&document.documentElement.scrollWidth<=innerWidth)).toBe(true);
  const frame=await page.getByTitle('3Dアバター作成').boundingBox();
  expect(frame!.height).toBeGreaterThan(180);expect(frame!.y+frame!.height).toBeLessThanOrEqual(viewport.height);
  await expect(page.getByRole('link',{name:'TOPへ戻る',exact:true})).toBeInViewport();
  await page.mouse.move(8,viewport.height/2);await page.mouse.wheel(0,800);
  expect(await page.evaluate(()=>scrollY)).toBe(0);
  await page.screenshot({path:`/private/tmp/avatar-fixed-${viewport.width}-${viewport.height}.png`});
 });
}
