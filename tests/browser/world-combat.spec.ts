import {test,expect,type FrameLocator} from '@playwright/test';
type State={panel:string;combatAvailable:boolean;combatActive:boolean;avatarChoice?:{baseAvatarId:string}};
async function snapshot(world:FrameLocator):Promise<State>{
 return world.locator('body').evaluate(async()=>{
  const w=window as unknown as {unityInstance:{SendMessage:(object:string,method:string)=>void};melifeWorldInteraction?:State};
  delete w.melifeWorldInteraction;w.unityInstance.SendMessage('MiraiLife','ReportWebInteraction');
  for(let i=0;i<60&&!w.melifeWorldInteraction;i++)await new Promise(r=>setTimeout(r,16));
  if(!w.melifeWorldInteraction)throw Error('World receipt missing');return w.melifeWorldInteraction;
 });
}

for(const v3 of [false,true])test(`F toggles combat with ${v3?'V3':'guest'} avatar`,async({page})=>{
 await page.setViewportSize({width:1440,height:900});
 if(v3){
  const owner='11111111-1111-4111-8111-111111111111';
  await page.route('**/api/unity/session',route=>route.fulfill({json:{authenticated:true,profile:{userId:owner,nickname:'戦闘確認',avatarId:'cat',gender:'male',ageBadge:'ユース',teaches:[],wantsToLearn:[]}}}));
  await page.addInitScript(owner=>localStorage.setItem('melife.demo-profile.v1',JSON.stringify({version:1,id:owner,nickname:'戦闘確認',appearance:{version:3,assetProfile:'v3-unified-20261104',choice:{baseAvatarId:'young-man',body:0,face:0,hair:0,outerwear:0,pants:0,shoes:0,glasses:-1,accessory:-1,costume:-1,hairColor:'#60412E',topColor:'#2563EB',pantsColor:'#627B99',skinColor:''}}})),owner);
 }
 await page.goto('/plaza');const world=page.frameLocator('iframe[title="ミライフ Unityワールド"]');
 await expect(world.locator('body')).toHaveAttribute('data-unity-state','ready',{timeout:150000});
 await page.waitForTimeout(4000);await world.locator('canvas').focus();
 let ready=await snapshot(world);
 for(let i=0;i<3&&ready.panel!=='None';i++){await page.keyboard.press('Enter',{delay:150});ready=await snapshot(world);}
 expect(ready.panel).toBe('None');expect(ready.combatAvailable).toBe(true);
 if(v3)expect(ready.avatarChoice?.baseAvatarId).toBe('young-man');
 await page.keyboard.press('f',{delay:150});expect((await snapshot(world)).combatActive).toBe(true);
 await page.keyboard.press('f',{delay:150});expect((await snapshot(world)).combatActive).toBe(false);
});
