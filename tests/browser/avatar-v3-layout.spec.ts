import {test,expect,type Page} from '@playwright/test';
import {COLOR_PALETTE} from '../../shared/avatar-types';
import {avatarUser,mockPreview} from '../support/avatar-v3';
async function fits(page:Page){
 expect(await page.evaluate(()=>({page:document.documentElement.scrollHeight<=innerHeight+1,width:document.documentElement.scrollWidth<=innerWidth+1,panels:[...document.querySelectorAll('.avatar-main-container,.avatar-editor-section,.avatar-options-grid')].every(node=>node.scrollHeight<=node.clientHeight+1)}))).toEqual({page:true,width:true,panels:true});
 for(const thumbnail of await page.locator('.avatar-part-image:visible,.avatar-thumbnail:visible').all())expect((await thumbnail.boundingBox())!.height).toBeGreaterThanOrEqual(24);
 for(const button of await page.locator('.avatar-editor-section button:visible').all()){
  const rect=(await button.boundingBox())!;expect(rect.y).toBeGreaterThanOrEqual(0);expect(rect.y+rect.height).toBeLessThanOrEqual((await page.viewportSize())!.height+1);
 }
}
for(const viewport of [{width:1440,height:900},{width:1024,height:600},{width:390,height:844},{width:844,height:390}])test(`wardrobe has no scrolling at ${viewport.width}x${viewport.height}`,async({page})=>{
 await page.setViewportSize(viewport);const owner=await avatarUser(page);await mockPreview(page);await page.goto('/avatar');await expect(page.locator('.avatar-base-card').first()).toBeEnabled();await fits(page);
 await page.getByRole('button',{name:'このアバターで編集へ →'}).click();
 for(const name of ['上着・服','ズボン','髪型','表情','靴','メガネ','小物','色の調整','コスチューム']){
  await page.locator('.avatar-tabs').getByRole('button',{name,exact:true}).click();await fits(page);
  await expect(page.getByRole('button',{name:'このアバターで街へ行く'})).toBeVisible();
 }
 await page.locator('.avatar-tabs').getByRole('button',{name:'小物',exact:true}).click();
 await expect(page.locator('.avatar-option-card:visible')).toHaveCount(3);await page.getByRole('button',{name:'次の小物'}).click();await expect(page.locator('.avatar-option-card:visible')).toHaveCount(3);await fits(page);
 await page.locator('.avatar-option-card').last().click();
 expect(await page.evaluate(owner=>JSON.parse(localStorage.getItem('melife.avatar.v3.draft:'+owner)!).choice.accessory,owner)).toBe(4);
 await page.locator('.avatar-tabs').getByRole('button',{name:'メガネ',exact:true}).click();await page.locator('.avatar-option-card').last().click();
 expect(await page.evaluate(owner=>JSON.parse(localStorage.getItem('melife.avatar.v3.draft:'+owner)!).choice.glasses,owner)).toBe(1);
 await page.locator('.avatar-tabs').getByRole('button',{name:'色の調整',exact:true}).click();
 await expect(page.locator('.avatar-color-settings .avatar-color-group:visible')).toHaveCount(4);await fits(page);
 await expect(page.locator('.avatar-color-targets')).toHaveCount(0);
 for(const [label,field,colors] of [['上着の色','topColor',COLOR_PALETTE.tops],['ズボンの色','pantsColor',COLOR_PALETTE.pants],['髪色','hairColor',COLOR_PALETTE.hair]] as const){
  const group=page.locator('.avatar-color-settings .avatar-color-group').filter({has:page.getByText(label,{exact:true})});await group.getByRole('button').last().click();
  await expect.poll(()=>page.evaluate(({owner,field})=>JSON.parse(localStorage.getItem('melife.avatar.v3.draft:'+owner)!).choice[field],{owner,field})).toBe(colors.at(-1)!.hex);
 }
 await page.screenshot({path:test.info().outputPath(`fixed-colors-${viewport.width}.png`),fullPage:true,animations:'disabled'});
 await page.locator('.avatar-tabs').getByRole('button',{name:'小物',exact:true}).click();
 await page.screenshot({path:test.info().outputPath(`fixed-${viewport.width}.png`),fullPage:true,animations:'disabled'});
});
