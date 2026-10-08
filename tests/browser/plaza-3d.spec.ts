import {test,expect,Page} from '@playwright/test';

async function enter(page:Page,name='3Dテスト'){
 await page.addInitScript(()=>{
  const Original=window.WebSocket;
  window.WebSocket=class extends Original{constructor(url:string|URL,protocols?:string|string[]){const u=new URL(url.toString());if(u.port==='3003')u.port='3005';super(u.toString(),protocols);}};
 });
 await page.goto('/plaza');await page.getByLabel('広場での名前').fill(name);await page.getByRole('button',{name:'広場に入る'}).click();
 await expect(page.getByRole('status')).toContainText('オンライン');
}
test('3D広場と4室を表示し、名前と入口がサイズ変更に追従する',async({page})=>{
 const errors:string[]=[];page.on('pageerror',e=>errors.push(e.message));
 await enter(page);
 const canvas=page.getByRole('img',{name:'ミライフ広場の3Dマップ'});
 await expect(canvas).toBeVisible();
 await expect(page.locator('.plaza-person.is-self')).toHaveAttribute('data-world-x',/\d/);
 const door=page.getByRole('button',{name:'交流カフェに入る'});
 const before=await door.boundingBox();
 await page.setViewportSize({width:1024,height:768});
 await expect.poll(async()=>Math.round((await door.boundingBox())!.x)).not.toBe(Math.round(before!.x));
 const map=await page.locator('.plaza-map').boundingBox();const after=await door.boundingBox();
 expect(after!.x).toBeGreaterThanOrEqual(map!.x);expect(after!.x+after!.width).toBeLessThanOrEqual(map!.x+map!.width);
 await page.setViewportSize({width:1440,height:1000});
 await page.screenshot({path:'/tmp/melife-3d-lobby.png',fullPage:true});
 for(const room of ['交流カフェ','趣味の部屋','学びの部屋','経験共有の部屋']){
  await page.getByRole('button',{name:`${room}に入る`}).click();
  await expect(page.getByRole('img',{name:`${room}の3Dマップ`})).toBeVisible();
  await expect(page.locator('.plaza-person')).toHaveCount(1);
  await page.screenshot({path:`/tmp/melife-3d-${room}.png`,fullPage:true});
  await page.getByRole('button',{name:'ロビーへ →'}).click();
  await expect(page.getByRole('img',{name:'ミライフ広場の3Dマップ'})).toBeVisible();
 }
 await page.getByRole('button',{name:'退出する ↗'}).click();expect(errors).toEqual([]);
});
