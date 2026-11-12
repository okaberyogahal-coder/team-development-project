import {test,expect} from '@playwright/test';
import {avatarUser} from '../support/avatar-v3';
import {DEMO_PROFILE_KEY} from '../../shared/demo-profile';
test('real six-model wardrobe applies the same appearance in existing city and read-only My Page',async({page})=>{
 test.setTimeout(300000);await avatarUser(page);
 const errors:string[]=[];page.on('pageerror',error=>errors.push(error.message));
 await page.goto('/avatar');
 const preview=page.frameLocator('iframe[title="3Dアバタープレビュー"]');
 await expect(preview.locator('body')).toHaveAttribute('data-unity-state','ready',{timeout:150000});
 for(let index=0;index<6;index++){
  const card=page.locator('.avatar-base-card').nth(index);await expect(card).toBeEnabled();await card.click();
  await expect.poll(()=>preview.locator('body').evaluate(()=>{const state=(window as unknown as {miraifuAppliedAvatar?:{choice:{baseAvatarId:string};pairMatched:boolean}}).miraifuAppliedAvatar;return state?.pairMatched&&state.choice.baseAvatarId;})).toBe(['young-man','young-woman','middle-aged-man','middle-aged-woman','elderly-man','elderly-woman'][index]);
 }
 await page.getByRole('button',{name:'このアバターで編集へ →'}).click();
 await page.getByRole('button',{name:'シック',exact:true}).click();
 await page.locator('.avatar-tabs').getByRole('button',{name:'小物',exact:true}).click();await page.getByRole('button',{name:'次の小物'}).click();await page.locator('.avatar-option-card').last().click();
 await page.locator('.avatar-tabs').getByRole('button',{name:'メガネ',exact:true}).click();await page.locator('.avatar-option-card').last().click();
 const complete=page.getByRole('button',{name:'このアバターで街へ行く'});await expect(complete).toBeEnabled();
 const applied=await preview.locator('body').evaluate(()=>(window as unknown as {miraifuAppliedAvatar:{choice:unknown}}).miraifuAppliedAvatar.choice);
 await page.screenshot({path:test.info().outputPath('real-member-wardrobe.png')});
 await complete.click();await expect(page).toHaveURL(/\/plaza$/);
 const world=page.frameLocator('iframe[title="ミライフ Unityワールド"]');
 await expect(world.locator('body')).toHaveAttribute('data-unity-state','ready',{timeout:150000});
 await page.waitForTimeout(1000);
 await expect.poll(()=>world.locator('body').evaluate(()=>(window as unknown as {melifeAppliedAvatar?:{choice:unknown}}).melifeAppliedAvatar?.choice),{timeout:25000}).toEqual(applied);
 const saved=await page.evaluate(key=>localStorage.getItem(key),DEMO_PROFILE_KEY);
 const snapshot=()=>world.locator('body').evaluate(async()=>{
  const w=window as unknown as {unityInstance:{SendMessage:(object:string,method:string)=>void};melifeWorldInteraction?:{position:{x:number;z:number};yaw:number;panel:string;avatarChoice:unknown}};
  delete w.melifeWorldInteraction;w.unityInstance.SendMessage('MiraiLife','ReportWebInteraction');
  for(let i=0;i<60&&!w.melifeWorldInteraction;i++)await new Promise(r=>setTimeout(r,16));
  return w.melifeWorldInteraction!;
 });
 await page.waitForTimeout(4000);
 const canvas=world.locator('canvas');await canvas.click({position:{x:100,y:120}});
 const before=await snapshot();expect(before.avatarChoice).toEqual(applied);
 await page.keyboard.down('ArrowUp');await page.waitForTimeout(800);await page.keyboard.up('ArrowUp');const after=await snapshot();
 expect(Math.hypot(after.position.x-before.position.x,after.position.z-before.position.z)).toBeGreaterThan(.4);
 await page.getByRole('button',{name:'全画面表示',exact:true}).click();await expect(page.getByRole('button',{name:'元に戻す',exact:true})).toBeVisible();
 await canvas.click({position:{x:100,y:120}});await page.keyboard.down('w');await page.waitForTimeout(600);await page.keyboard.up('w');const full=await snapshot();
 expect(Math.hypot(full.position.x-after.position.x,full.position.z-after.position.z)).toBeGreaterThan(.3);
 await page.getByRole('button',{name:'元に戻す',exact:true}).click();
 await page.screenshot({path:test.info().outputPath('real-current-world.png')});
 await page.goto('/account');
 const account=page.frameLocator('iframe[title="現在の3Dアバター"]');
 await expect(page.locator('.account-avatar-stage')).toHaveAttribute('aria-busy','false',{timeout:150000});
 expect(await account.locator('body').evaluate(()=>(window as unknown as {melifeAppliedAvatar:{choice:unknown}}).melifeAppliedAvatar.choice)).toEqual(applied);
 expect(await page.evaluate(key=>localStorage.getItem(key),DEMO_PROFILE_KEY)).toBe(saved);
 await expect.poll(()=>account.locator('canvas').evaluate(canvas=>new Promise<number>(resolve=>requestAnimationFrame(()=>{
  const c=canvas as HTMLCanvasElement,gl=c.getContext('webgl2')!;const pixels=new Uint8Array(c.width*c.height*4);gl.readPixels(0,0,c.width,c.height,gl.RGBA,gl.UNSIGNED_BYTE,pixels);
  let dark=0,sky=0;for(let i=0;i<pixels.length;i+=4){if(pixels[i]<100&&pixels[i+1]<120&&pixels[i+2]<150)dark++;if(pixels[i]>220&&pixels[i+1]>235&&pixels[i+2]>245)sky++;}
  resolve(sky>c.width*c.height*.6?dark:0);
 }))),{timeout:30000}).toBeGreaterThan(500);
 await page.screenshot({path:test.info().outputPath('real-account-v3.png')});
 expect(errors).toEqual([]);
});
test('real world clears V3 on logout and never applies the previous owners outfit to a new user',async({page})=>{
 const owner=await avatarUser(page);
 const {DEFAULT_AVATAR_CHOICE}=await import('../../shared/avatar-types');
 await page.addInitScript(({owner,key,choice})=>{if(top===window)localStorage.setItem(key,JSON.stringify({version:1,id:owner,nickname:'着せ替え確認',appearance:{version:3,assetProfile:'v3-unified-20261104',choice}}));},{owner,key:DEMO_PROFILE_KEY,choice:{...DEFAULT_AVATAR_CHOICE,baseAvatarId:'young-woman'}});
 await page.goto('/plaza');const world=page.frameLocator('iframe[title="ミライフ Unityワールド"]');
 await expect.poll(()=>world.locator('body').evaluate(()=>(window as unknown as {melifeAppliedAvatar?:{choice:{baseAvatarId:string}}}).melifeAppliedAvatar?.choice.baseAvatarId),{timeout:150000}).toBe('young-woman');
 const actual=()=>world.locator('body').evaluate(async()=>{
  const w=window as unknown as {unityInstance:{SendMessage:(object:string,method:string)=>void};melifeWorldInteraction?:{avatarChoice:unknown}};delete w.melifeWorldInteraction;w.unityInstance.SendMessage('MiraiLife','ReportWebInteraction');for(let i=0;i<60&&!w.melifeWorldInteraction;i++)await new Promise(r=>setTimeout(r,16));return ((w.melifeWorldInteraction as {avatarChoice:{baseAvatarId?:string}|null}|undefined)?.avatarChoice)?.baseAvatarId||null;
 });
 await page.getByRole('button',{name:'ログアウト',exact:true}).click();await expect(world.locator('body')).toHaveAttribute('data-session-state','guest');
 await expect.poll(actual).toBeNull();
 const next=await avatarUser(page);expect(next).not.toBe(owner);await page.reload();
 await expect(world.locator('body')).toHaveAttribute('data-session-state','authenticated',{timeout:150000});
 await expect.poll(actual).toBeNull();
 expect(await page.evaluate(key=>JSON.parse(localStorage.getItem(key)!).id,DEMO_PROFILE_KEY)).toBe(owner);
});
