import {test,expect,type FrameLocator} from '@playwright/test';
import {readFileSync} from 'node:fs';

type State={panel:string;roomId:string|null;topicId:string|null;position:{x:number;y:number;z:number}};
const catalog=JSON.parse(readFileSync('unity/FutureCity/Assets/FutureCity/Resources/RoomTopics.json','utf8')) as {rooms:{roomId:string;topics:{id:string}[]}[]};
async function snapshot(world:FrameLocator):Promise<State>{
 return world.locator('body').evaluate(async()=>{
  const w=window as unknown as {unityInstance:{SendMessage:(object:string,method:string)=>void};melifeWorldInteraction?:State};
  delete w.melifeWorldInteraction;w.unityInstance.SendMessage('MiraiLife','ReportWebInteraction');
  for(let i=0;i<60&&!w.melifeWorldInteraction;i++)await new Promise(r=>setTimeout(r,16));
  if(!w.melifeWorldInteraction)throw new Error('World receipt missing');return w.melifeWorldInteraction;
 });
}
test('all four rooms expose their five usable topics and remember independent choices',async({page},info)=>{
 const errors:string[]=[];page.on('pageerror',error=>errors.push(error.message));
 await page.goto('/plaza');
 const world=page.frameLocator('iframe[title="ミライフ Unityワールド"]');
 await expect(world.locator('body')).toHaveAttribute('data-unity-state','ready',{timeout:150000});
 await page.waitForTimeout(4000);const canvas=world.locator('canvas');await canvas.focus();await page.keyboard.press('Enter',{delay:150});
 await expect.poll(async()=>(await snapshot(world)).panel).toBe('None');
 const bounds=(await canvas.boundingBox())!;
 const scale=Math.max(.6,bounds.height/1080)*1.15;
 const W=bounds.width/scale,H=bounds.height/scale;
 const body=(width:number,height:number)=>{const w=Math.min(width,W-30),h=Math.min(height,H-30);return {x:(W-w)/2+30,y:(H-h)/2+96,width:w-60,height:h-120};};
 const click=async(x:number,y:number)=>{await canvas.click({position:{x:x*scale,y:y*scale},delay:100});};
 const roomIds=['learn','story','cafe','hobby'];
 async function enter(index:number){
  await canvas.focus();await page.keyboard.press('3',{delay:150});await expect.poll(async()=>(await snapshot(world)).panel).toBe('Connect');
  const c=body(1180,860);await click(c.x+c.width-162,c.y+70+index*124+56);
  await expect.poll(async()=>(await snapshot(world)).panel).toBe('RoomTopics');
  expect((await snapshot(world)).roomId).toBe(roomIds[index]);
 }
 for(let index=0;index<roomIds.length;index++){
  await enter(index);const room=catalog.rooms.find(r=>r.roomId===roomIds[index])!;
  const before=await snapshot(world);await page.keyboard.down('ArrowUp');await page.waitForTimeout(250);await page.keyboard.up('ArrowUp');
  const still=await snapshot(world);expect(Math.hypot(still.position.x-before.position.x,still.position.z-before.position.z)).toBeLessThan(.01);
  for(let i=0;i<room.topics.length;i++){
   const c=body(1080,850);
   const sx=bounds.x+(c.x+c.width-8)*scale;
   await page.mouse.move(sx,bounds.y+(c.y+c.height/2)*scale);
   await page.mouse.down();await page.mouse.move(sx,bounds.y+(c.y+c.height-2)*scale,{steps:12});await page.mouse.up();
   await page.waitForTimeout(200);
   // At the scroll end, the footer is 60px high plus 20px padding; rows are 64px high.
   await click(c.x+c.width/2,c.y+c.height-116-(4-i)*64);
   await expect.poll(async()=>(await snapshot(world)).topicId).toBe(room.topics[i].id);
  }
  await page.screenshot({path:info.outputPath(roomIds[index]+'-topic.png')});
  await page.keyboard.press('Escape',{delay:150});await expect.poll(async()=>(await snapshot(world)).panel).toBe('None');
  await click(231,184);await expect.poll(async()=>(await snapshot(world)).panel).toBe('RoomTopics');
  expect((await snapshot(world)).topicId).toBe(room.topics[4].id);
  await page.keyboard.press('Escape',{delay:150});await click(231,238);
  await expect.poll(async()=>(await snapshot(world)).roomId).toBeFalsy();
 }
 await enter(2);expect((await snapshot(world)).topicId).toBe(catalog.rooms.find(r=>r.roomId==='cafe')!.topics[4].id);
 await page.keyboard.press('Escape',{delay:150});const before=await snapshot(world);
 await page.keyboard.down('ArrowUp');await page.waitForTimeout(300);await page.keyboard.up('ArrowUp');const after=await snapshot(world);
 expect(Math.hypot(after.position.x-before.position.x,after.position.z-before.position.z)).toBeGreaterThan(.05);
 expect(errors).toEqual([]);
});
