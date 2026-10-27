import {test,expect,type FrameLocator} from '@playwright/test';
type Snapshot={panel:string;position:{x:number;y:number;z:number};yaw:number;cursorVisible:boolean;freeLook:boolean};
async function snapshot(world:FrameLocator):Promise<Snapshot>{
 return world.locator('body').evaluate(async()=>{
  const w=window as unknown as Window&{unityInstance:{SendMessage:(object:string,method:string)=>void};melifeWorldInteraction?:Snapshot};
  delete w.melifeWorldInteraction;
  w.unityInstance.SendMessage('MiraiLife','ReportWebInteraction');
  for(let i=0;i<60&&!w.melifeWorldInteraction;i++)await new Promise(r=>setTimeout(r,16));
  if(!w.melifeWorldInteraction)throw new Error('Unity interaction receipt missing');
  return w.melifeWorldInteraction;
 });
}
for(const viewport of [{width:1440,height:900},{width:1024,height:600},{width:390,height:844}]){
 test(`normal world fills viewport ${viewport.width}x${viewport.height} without repeated headers`,async({page})=>{
  await page.setViewportSize(viewport);
  await page.route('**/unity/FutureCity/index.html*',route=>route.fulfill({contentType:'text/html',body:'<!doctype html>'}));
  await page.goto('/plaza');
  const rect=await page.getByTitle('ミライフ Unityワールド').boundingBox();
  expect(rect!.height).toBeGreaterThanOrEqual(viewport.height*.9);
  expect(await page.evaluate(()=>document.documentElement.scrollHeight<=innerHeight&&document.documentElement.scrollWidth<=innerWidth)).toBe(true);
 });
}
test('visible cursor selects Profile, arrow keys walk, drag looks around and fullscreen keeps input',async({page})=>{
 test.setTimeout(180000);
 await page.goto('/plaza');
 const world=page.frameLocator('iframe[title="ミライフ Unityワールド"]');
 await expect(world.locator('body')).toHaveAttribute('data-unity-state','ready',{timeout:150000});
 await expect.poll(async()=>(await snapshot(world)).panel).toBe('Intro');
 // Unity's runtime is callable before its splash animation releases input.
 await page.waitForTimeout(4000);
 const canvas=world.locator('canvas');
 await canvas.focus();await page.keyboard.press('Enter',{delay:150});
 await expect.poll(async()=>(await snapshot(world)).panel).toBe('None');
 expect((await snapshot(world)).freeLook).toBe(false);
 expect((await snapshot(world)).cursorVisible).toBe(true);
 let rect=(await canvas.boundingBox())!;
 await canvas.click({position:{x:rect.width*.665,y:rect.height-30},delay:150});
 await expect.poll(async()=>(await snapshot(world)).panel).toBe('Profile');
 await page.keyboard.press('Escape',{delay:150});
 await expect.poll(async()=>(await snapshot(world)).panel).toBe('None');
 const before=await snapshot(world);
 await page.keyboard.down('ArrowUp');await page.waitForTimeout(800);await page.keyboard.up('ArrowUp');
 const after=await snapshot(world);
 expect(Math.hypot(after.position.x-before.position.x,after.position.z-before.position.z)).toBeGreaterThan(.5);
 await page.mouse.move(rect.x+rect.width*.5,rect.y+rect.height*.6);
 await page.mouse.down({button:'right'});await page.mouse.move(rect.x+rect.width*.6,rect.y+rect.height*.6,{steps:12});await page.mouse.up({button:'right'});
 expect(Math.abs((await snapshot(world)).yaw-after.yaw)).toBeGreaterThan(1);
 await page.getByRole('button',{name:'全画面表示',exact:true}).click();
 await canvas.click({position:{x:100,y:120}});
 const fullBefore=await snapshot(world);
 await page.keyboard.down('ArrowRight');await page.waitForTimeout(600);await page.keyboard.up('ArrowRight');
 const fullAfter=await snapshot(world);
 expect(Math.hypot(fullAfter.position.x-fullBefore.position.x,fullAfter.position.z-fullBefore.position.z)).toBeGreaterThan(.4);
 await page.getByRole('button',{name:'元に戻す',exact:true}).click();
 await page.screenshot({path:'/private/tmp/melife-world-compact.png'});
});
