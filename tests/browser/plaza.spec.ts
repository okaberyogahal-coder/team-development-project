import {test,expect} from '@playwright/test';
function isolate(){const Original=window.WebSocket;window.WebSocket=class extends Original{constructor(url:string|URL,protocols?:string|string[]){const endpoint=new URL(url.toString());if(endpoint.port==='3003')endpoint.port='3005';super(endpoint.toString(),protocols);}};}
test.beforeEach(async({page})=>{await page.addInitScript(isolate);});
test('入場・アバター移動・部屋変更・退出を2ブラウザで確認',async({browser,page})=>{
 const errors:string[]=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto('/plaza');await expect(page.getByRole('heading',{name:'あなたらしく、入場しよう'})).toBeVisible();
 await page.screenshot({path:'/tmp/melife-plaza-entry.png',fullPage:true});
 await page.getByLabel('広場での名前').fill('はな');await page.getByRole('button',{name:'広場に入る'}).click();
 await expect(page.getByRole('button',{name:/参加者/})).toContainText('1人');
 const context=await browser.newContext({viewport:{width:1440,height:1000}});await context.addInitScript(isolate);const other=await context.newPage();
 try{
  await other.goto('/plaza');await other.getByLabel('広場での名前').fill('たろう');await other.getByRole('button',{name:'広場に入る'}).click();
  await expect(page.getByRole('button',{name:/参加者/})).toContainText('2人');
  const person=page.locator('.plaza-person.is-self');await expect(person).toHaveAttribute('data-world-x',/\d/);const before=Number(await person.getAttribute('data-world-x'));
  await page.locator('.plaza-space-heading h2').click();await page.keyboard.down('ArrowRight');await expect.poll(async()=>Number(await person.getAttribute('data-world-x'))).toBeGreaterThan(before+5);await page.keyboard.up('ArrowRight');
  const remote=other.locator('.plaza-person').filter({hasText:'はな'});
  await expect.poll(async()=>Number(await remote.getAttribute('data-world-x'))).toBeGreaterThan(before+5);
  const right=Number(await person.getAttribute('data-world-x'));const screenRight=(await person.boundingBox())!.x;
  await page.keyboard.down('ArrowLeft');await expect.poll(async()=>Number(await person.getAttribute('data-world-x'))).toBeLessThan(right-5);await page.keyboard.up('ArrowLeft');
  await expect.poll(async()=>(await person.boundingBox())!.x).toBeLessThan(screenRight);
  await expect.poll(async()=>Math.abs(Number(await remote.getAttribute('data-world-x'))-Number(await person.getAttribute('data-world-x')))).toBeLessThan(2);
  await page.screenshot({path:'/tmp/melife-plaza-lobby.png',fullPage:true});
  await page.getByRole('button',{name:'交流カフェに入る'}).click();await expect(page.locator('.plaza-space-heading h2')).toHaveText('交流カフェ');
  await expect(page.getByText('ビデオ通話は準備中です',{exact:true})).toBeVisible();
  await expect(page.getByRole('button',{name:/参加者/})).toContainText('1人');await expect(other.getByRole('button',{name:/参加者/})).toContainText('1人');
  await page.screenshot({path:'/tmp/melife-plaza-room.png',fullPage:true});
  await page.getByRole('button',{name:'ロビーへ →'}).click();await expect(page.getByRole('button',{name:/参加者/})).toContainText('2人');
  await page.getByRole('button',{name:'退出する ↗'}).click();await expect(page.getByRole('heading',{name:'あなたらしく、入場しよう'})).toBeVisible();
  await other.getByRole('button',{name:'退出する ↗'}).click();assertNoErrors();
 }finally{await context.close();}
 function assertNoErrors(){expect(errors).toEqual([]);}
});
test('キー長押しの繰り返し入力でも通信制限で切断しない',async({page})=>{
 await page.addInitScript(()=>{const Original=window.WebSocket;(window as any).__plazaCloseCount=0;window.WebSocket=class extends Original{constructor(url:string|URL,protocols?:string|string[]){super(url,protocols);this.addEventListener('close',()=>{(window as any).__plazaCloseCount++;});}};});
 await page.goto('/plaza');await page.getByLabel('広場での名前').fill('長押しテスト');await page.getByRole('button',{name:'広場に入る'}).click();await expect(page.getByRole('button',{name:/参加者/})).toContainText('1人');
 await page.locator('.plaza-space-heading h2').click();
 await page.evaluate(async()=>{for(let i=0;i<60;i++){document.body.dispatchEvent(new KeyboardEvent('keydown',{key:'ArrowRight',repeat:i>0,bubbles:true}));await new Promise(r=>setTimeout(r,20));}document.body.dispatchEvent(new KeyboardEvent('keyup',{key:'ArrowRight',bubbles:true}));});
 expect(await page.evaluate(()=>(window as any).__plazaCloseCount)).toBe(0);
 await expect(page.locator('.plaza-live-status')).toContainText('オンライン');await expect(page.locator('.plaza-live-status')).not.toContainText('再接続');await page.getByRole('button',{name:'退出する ↗'}).click();
});
test('LANのIPアドレスから参加したアバターとlocalhostの参加者が同じ部屋を共有する',async({browser,page})=>{
 const {networkInterfaces}=await import('node:os');const {lanConfig}=await import('../../scripts/lan-config.mjs');
 const config=lanConfig(networkInterfaces(),Number(new URL(test.info().project.use.baseURL!).port));test.skip(!config.urls.length,'LANアドレスなし');
 await page.goto('/plaza');await page.getByLabel('広場での名前').fill('このPC');await page.getByRole('button',{name:'広場に入る'}).click();await expect(page.getByRole('button',{name:/参加者/})).toContainText('1人');
 const context=await browser.newContext();await context.addInitScript(isolate);const other=await context.newPage();
 try{
  await other.goto(config.urls[0]);await other.getByLabel('広場での名前').fill('LANから参加');await other.getByRole('button',{name:'広場に入る'}).click();
  await expect(other.getByRole('button',{name:/参加者/})).toContainText('2人');await expect(page.getByRole('button',{name:/参加者/})).toContainText('2人');
  await other.getByRole('button',{name:'交流カフェに入る'}).click();await expect(other.locator('.plaza-space-heading h2')).toHaveText('交流カフェ');
  await page.getByRole('button',{name:'交流カフェに入る'}).click();await expect(other.getByRole('button',{name:/参加者/})).toContainText('2人');
  await other.getByRole('button',{name:'退出する ↗'}).click();await page.getByRole('button',{name:'退出する ↗'}).click();
 }finally{await context.close();}
});

test.afterEach(async({page})=>{const exit=page.getByRole('button',{name:'退出する ↗'});if(await exit.isVisible()){await exit.click();await expect(page.getByRole('button',{name:'広場に入る'})).toBeVisible();}});
