import {test,expect,Page} from '@playwright/test';

async function enter(page:Page,name='3Dテスト'){
 await page.addInitScript(()=>{
  const Original=window.WebSocket;
  window.WebSocket=class extends Original{constructor(url:string|URL,protocols?:string|string[]){const u=new URL(url.toString());if(u.port==='3003')u.port='3005';super(u.toString(),protocols);}};
 });
 await page.goto('/plaza');await page.getByLabel('広場での名前').fill(name);await page.getByRole('button',{name:'広場に入る'}).click();
 await expect(page.locator('.plaza-live-status')).toContainText('オンライン');
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

test('WebGLが使えない端末でも2Dで部屋へ移動できる',async({page})=>{
 await page.addInitScript(()=>{
  const original=HTMLCanvasElement.prototype.getContext;
  HTMLCanvasElement.prototype.getContext=function(this:HTMLCanvasElement,kind:string,...args:any[]){if(kind==='webgl' || kind==='webgl2' || kind==='experimental-webgl')return null;return original.apply(this,[kind,...args] as any);} as any;
 });
 await enter(page);
 await expect(page.getByText('この環境では3D表示を利用できません。')).toBeVisible();
 await page.getByRole('button',{name:'2Dで表示'}).click();
 await expect(page.getByRole('img',{name:/ミライフ広場のマップ/})).toBeVisible();
 await page.getByRole('button',{name:'交流カフェに入る'}).click();
 await expect(page.locator('.plaza-space-heading h2')).toHaveText('交流カフェ');
 await expect(page.getByRole('img',{name:/交流カフェのマップ/})).toBeVisible();
});
test('3Dの接続が失われても2Dに切り替えられる',async({page})=>{
 await enter(page);await expect(page.locator('.plaza-world3d canvas')).toBeVisible();
 await page.locator('.plaza-world3d canvas').evaluate(canvas=>canvas.dispatchEvent(new Event('webglcontextlost',{cancelable:true})));
 await expect(page.getByRole('button',{name:'2Dで表示'})).toBeVisible();
 await page.getByRole('button',{name:'2Dで表示'}).click();
 await expect(page.locator('.plaza-map-svg')).toBeVisible();await expect(page.locator('.plaza-world3d canvas')).toHaveCount(0);
});

test('入力中とフォーカス喪失時は歩行が止まる',async({page})=>{
 await enter(page);const person=page.locator('.plaza-person.is-self');
 await expect(person).toHaveAttribute('data-world-x',/\d/);
 await page.keyboard.down('ArrowRight');
 await expect.poll(async()=>Number(await person.getAttribute('data-world-x'))).toBeGreaterThan(515);
 await page.evaluate(()=>window.dispatchEvent(new Event('blur')));await page.keyboard.up('ArrowRight');
 await page.waitForTimeout(350);const stopped=Number(await person.getAttribute('data-world-x'));
 await page.waitForTimeout(250);expect(Math.abs(Number(await person.getAttribute('data-world-x'))-stopped)).toBeLessThan(1);
 await page.evaluate(()=>{const input=document.createElement('input');input.setAttribute('aria-label','入力停止テスト');document.body.appendChild(input);input.focus();});
 await page.keyboard.down('ArrowRight');await page.waitForTimeout(350);await page.keyboard.up('ArrowRight');
 expect(Math.abs(Number(await person.getAttribute('data-world-x'))-stopped)).toBeLessThan(1);
});

test.afterEach(async({page})=>{const exit=page.getByRole('button',{name:'退出する ↗'});if(await exit.isVisible()){await exit.click();await expect(page.getByRole('button',{name:'広場に入る'})).toBeVisible();}});
