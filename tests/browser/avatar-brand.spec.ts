import {expect,test} from '@playwright/test';
import {DEFAULT_AVATAR,DEMO_PROFILE_KEY} from '../../shared/demo-profile';

for(const width of [1440,390])test(`avatar shell matches TOP branding at ${width}px`,async({page})=>{
 await page.addInitScript(({key,appearance})=>localStorage.setItem(key,JSON.stringify({version:1,id:'brand-test',nickname:'みらい',appearance})),{key:DEMO_PROFILE_KEY,appearance:DEFAULT_AVATAR});
 // This test covers the surrounding HTML only; separate tests run the actual WebGL build.
 await page.route('**/unity/FutureCity/index.html*',route=>route.fulfill({contentType:'text/html',body:'<!doctype html><title>Shell fixture</title>'}));
 await page.setViewportSize({width,height:900});await page.goto('/avatar');
 await expect(page.locator('.avatar-setup-page')).toBeVisible();
 await expect(page.getByRole('heading',{name:'あなたらしい姿で、はじめよう。'})).toHaveCSS('color','rgb(21, 59, 120)');
 await expect(page.locator('.home-brand-logo-image')).toHaveAttribute('src',/milife-logo-matte/);
 await expect(page.getByRole('link',{name:'TOPへ戻る',exact:true})).toBeVisible();
 await expect(page.locator('.avatar-setup-onboarding')).toContainText('みらい');
 await expect(page.getByTitle('3Dアバター作成')).toBeVisible();
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
 await page.screenshot({path:`/private/tmp/avatar-shell-${width}.png`,fullPage:true});
 await page.goto('/plaza');await expect(page.locator('.avatar-setup-page')).toHaveCount(0);
});

test('rebuilt WebGL avatar theme renders and wardrobe controls still complete entry',async({page})=>{
 test.setTimeout(180000);
 await page.addInitScript(({key,appearance})=>{if(window.top===window&&!localStorage.getItem(key))localStorage.setItem(key,JSON.stringify({version:1,id:'brand-webgl',nickname:'みらい',appearance}));},{key:DEMO_PROFILE_KEY,appearance:DEFAULT_AVATAR});
 const errors:string[]=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto('/avatar');
 const world=page.frameLocator('iframe[title="3Dアバター作成"]');
 await expect(world.locator('body')).toHaveAttribute('data-unity-state','ready',{timeout:150000});
 await expect(world.locator('#loading')).toBeHidden();
 const canvas=world.locator('#unity-canvas');
 const box=(await canvas.boundingBox())!;
 // IMGUI controls are canvas-rendered. Coordinates follow the visible layout,
 // and assertions below check actual saved values, rather than assuming clicks worked.
 const s=Math.max(0.6,box.height/900);
 const W=box.width/s;const panelWidth=Math.min(1480,W-36);const left=(W-panelWidth)/2;
 const bodyX=left+28,bodyY=18+104;
 const inner=panelWidth-56-24;const previewW=Math.min(310,inner*0.27);
 const right=bodyX+previewW+32;const choiceWidth=inner-previewW-32;
 const tabWidth=(choiceWidth-24)/4;
 const click=async(x:number,y:number)=>{await canvas.click({position:{x:x*s,y:y*s},delay:120});};
 await expect.poll(async()=>canvas.evaluate(c=>new Promise<boolean>(resolve=>requestAnimationFrame(()=>{
  const canvas=c as HTMLCanvasElement;const gl=canvas.getContext('webgl2')!;const s=Math.max(.6,canvas.height/900);const W=canvas.width/s,pw=Math.min(1480,W-36);const left=(W-pw)/2;
  const p=new Uint8Array(4);gl.readPixels(Math.round((left+pw-340)*s),canvas.height-Math.round(52*s),1,1,gl.RGBA,gl.UNSIGNED_BYTE,p);resolve(p[2]>p[0]*2&&p[2]>p[1]*1.2&&p[3]>0);
 }))),{timeout:30000}).toBe(true);
 await page.screenshot({path:'/private/tmp/avatar-brand-real-desktop.png',fullPage:true});
 await click(bodyX+previewW*0.75,bodyY+Math.max(240,Math.min(420,(box.height/s-164)-210))+40);
 // Select preset 2, then customize it.
 const previewH=Math.max(240,Math.min(420,(box.height/s-164)-210));
 const presetW=(previewW-32)/5;
 await click(bodyX+(presetW+8)+presetW/2,bodyY+previewH+12+112+29);
 const choicesY=bodyY+58+18;
 const optionWidth=(choiceWidth-24)/4;
 await click(right+optionWidth+8+optionWidth/2,choicesY+42+28);
 const swatchW=(choiceWidth-56)/8;
 await click(right+swatchW+8+swatchW/2,choicesY+42+64+8+28);
 await page.screenshot({path:'/private/tmp/avatar-brand-real-clothes.png',fullPage:true});
 await click(right+tabWidth+8+tabWidth/2,bodyY+29);
 await click(right+optionWidth+8+optionWidth/2,choicesY+42+28);
 await click(right+2*(tabWidth+8)+tabWidth/2,bodyY+29);
 await click(right+optionWidth+8+optionWidth/2,choicesY+42+28);
 await click(right+3*(tabWidth+8)+tabWidth/2,bodyY+29);
 await click(right+choiceWidth/2,choicesY+100+96+40);
 await page.screenshot({path:'/private/tmp/avatar-brand-real-costume.png',fullPage:true});
 await click(left+panelWidth-194,18+22+31);
 await expect(page).toHaveURL(/\/plaza$/,{timeout:20000});
 const saved=await page.evaluate(key=>JSON.parse(localStorage.getItem(key)!),DEMO_PROFILE_KEY);
 expect(saved.appearance.parts[0]).toBe(1);expect(saved.appearance.colors[0]).toBe(1);
 expect(saved.appearance.parts[3]).toBe(1);expect(saved.appearance.parts[4]).toBe(1);expect(saved.appearance.costume).toBe(1);
 expect(errors).toEqual([]);
});

test.describe('small and Retina viewports',()=>{
 test.use({deviceScaleFactor:2});
 for(const viewport of [{width:1024,height:600},{width:390,height:844}])test(`actual wardrobe stays usable at ${viewport.width}x${viewport.height}`,async({page})=>{
  test.setTimeout(180000);await page.setViewportSize(viewport);
  await page.addInitScript(({key,appearance})=>{if(window.top===window&&!localStorage.getItem(key))localStorage.setItem(key,JSON.stringify({version:1,id:'small-brand',nickname:'みらい',appearance}));},{key:DEMO_PROFILE_KEY,appearance:DEFAULT_AVATAR});
  await page.goto('/avatar');const world=page.frameLocator('iframe[title="3Dアバター作成"]');
  await expect(world.locator('body')).toHaveAttribute('data-unity-state','ready',{timeout:150000});await expect(world.locator('#loading')).toBeHidden();
  const canvas=world.locator('#unity-canvas');
  await expect.poll(()=>canvas.evaluate(c=>new Promise<boolean>(resolve=>requestAnimationFrame(()=>{
   const cv=c as HTMLCanvasElement,gl=cv.getContext('webgl2')!;const scale=cv.width/cv.height<.9?cv.width/512:Math.max(.6,cv.height/900);const W=cv.width/scale,pw=Math.min(1480,W-36),left=(W-pw)/2;
   const narrow=W<820,x=narrow?left+46:left+pw-340,y=narrow?18+102:52;
   const p=new Uint8Array(4);gl.readPixels(Math.round(x*scale),cv.height-Math.round(y*scale),1,1,gl.RGBA,gl.UNSIGNED_BYTE,p);resolve(p[2]>p[0]*2&&p[2]>p[1]*1.2&&p[3]>0);
  }))),{timeout:30000}).toBe(true);
  const box=(await canvas.boundingBox())!;
  const density=await canvas.evaluate(c=>(c as HTMLCanvasElement).width/c.getBoundingClientRect().width);
  const s=(box.width/box.height<.9?box.width*density/512:Math.max(.6,box.height*density/900))/density;
  const W=box.width/s,pw=Math.min(1480,W-36),left=(W-pw)/2,inner=pw-80,narrow=W<820;
  const stacked=inner<780,previewW=stacked?Math.min(260,inner*.8):Math.min(310,inner*.27);
  const bodyX=left+28,bodyY=18+(narrow?174:104),right=bodyX+(stacked?0:previewW+32);
  const tabY=bodyY+(stacked?250+218:0),choiceY=tabY+(stacked?124:58)+18;
  const width=inner-(stacked?0:previewW+32),cols=Math.max(1,Math.min(4,Math.floor(width/150))),optionWidth=(width-(cols-1)*8)/cols;
  await page.screenshot({path:`/private/tmp/avatar-brand-real-${viewport.width}.png`,fullPage:true});
  if(viewport.width===390){
   // The page now fits the viewport. On portrait screens the lower choices
   // remain in the wardrobe's own scroll area; exercise a visible preset here.
   const presetWidth=(inner-32)/5;
   await canvas.click({position:{x:(bodyX+presetWidth+8+presetWidth/2)*s,y:(bodyY+250+12+112+29)*s},delay:120});
  }else{
  await canvas.click({position:{x:(right+(optionWidth+8)*(1%cols)+optionWidth/2)*s,y:(choiceY+42+Math.floor(1/cols)*64+28)*s},delay:120});
  const palette=width>=510?8:4,swatch=(width-(palette-1)*8)/palette;
  await canvas.click({position:{x:(right+swatch+8+swatch/2)*s,y:(choiceY+42+Math.ceil(4/cols)*64+8+28)*s},delay:120});
  }
  await canvas.hover({position:{x:box.width*.8,y:box.height*.8}});await page.mouse.wheel(0,400);
  await page.screenshot({path:`/private/tmp/avatar-brand-scrolled-${viewport.width}.png`,fullPage:true});
  const enter=narrow?{x:(left+pw/2)*s,y:(18+88+31)*s}:{x:(left+pw-194)*s,y:(18+22+31)*s};
  await canvas.click({position:enter,delay:120});await expect(page).toHaveURL(/\/plaza$/,{timeout:20000});
  const saved=await page.evaluate(key=>JSON.parse(localStorage.getItem(key)!),DEMO_PROFILE_KEY);
  if(viewport.width===390)expect(saved.appearance.baseVariant).toBe(1);
  else{expect(saved.appearance.parts[0]).toBe(1);expect(saved.appearance.colors[0]).toBe(1);}
 });
});
