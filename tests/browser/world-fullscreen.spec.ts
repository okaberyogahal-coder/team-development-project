import {expect,test} from '@playwright/test';

test.beforeEach(async({page})=>{
 await page.route('**/unity/FutureCity/index.html*',route=>route.fulfill({contentType:'text/html',body:'<!doctype html><title>World fixture</title>'}));
});

test('world enters fullscreen and restores with the button or browser exit without reloading Unity',async({page})=>{
 await page.goto('/plaza');
 const frame=page.getByTitle('ミライフ Unityワールド');
 await frame.evaluate(element=>(element as HTMLIFrameElement).contentWindow!.document.body.dataset.preserved='yes');
 const control=page.getByRole('button',{name:'全画面表示',exact:true});
 await control.click();
 await expect(page.getByRole('button',{name:'元に戻す',exact:true})).toHaveAttribute('aria-pressed','true');
 expect(await page.evaluate(()=>document.fullscreenElement?.className)).toBe('world-viewer');
 const bounds=await frame.boundingBox();
 const size=await page.evaluate(()=>({width:innerWidth,height:innerHeight}));
 expect(bounds!.width).toBe(size.width);
 expect(bounds!.y+bounds!.height).toBeLessThanOrEqual(size.height);
 await page.getByRole('button',{name:'元に戻す',exact:true}).click();
 await expect(control).toHaveAttribute('aria-pressed','false');
 expect(await page.evaluate(()=>document.fullscreenElement===null)).toBe(true);
 await control.click();
 await expect(page.getByRole('button',{name:'元に戻す',exact:true})).toBeVisible();
 // Simulate a browser-level exit (Escape is consumed by browser chrome in real use).
 await page.evaluate(()=>document.exitFullscreen());
 await expect(control).toHaveAttribute('aria-pressed','false');
 await expect(control).toBeFocused();
 expect(await frame.evaluate(element=>(element as HTMLIFrameElement).contentWindow!.document.body.dataset.preserved)).toBe('yes');
});

test('fullscreen request failure is explained and can be retried',async({page})=>{
 await page.goto('/plaza');
 await page.locator('.world-viewer').evaluate(element=>{element.requestFullscreen=()=>Promise.reject(new Error('denied'));});
 const control=page.getByRole('button',{name:'全画面表示',exact:true});
 await control.click();
 await expect(page.locator('.world-viewer [role=alert]')).toContainText('全画面表示を切り替えられませんでした');
 await expect(control).toBeEnabled();
 expect(await page.evaluate(()=>document.fullscreenElement===null)).toBe(true);
});

test('unsupported browsers explain why fullscreen is unavailable',async({page})=>{
 await page.goto('/plaza');
 await page.locator('.world-viewer').evaluate(element=>{Object.defineProperty(element,'requestFullscreen',{value:undefined});});
 await page.getByRole('button',{name:'全画面表示',exact:true}).click();
 await expect(page.locator('.world-viewer [role=alert]')).toContainText('このブラウザは全画面表示に対応していません');
});

test('real Unity continues running when fullscreen is entered and exited',async({page})=>{
 test.setTimeout(180000);
 await page.unroute('**/unity/FutureCity/index.html*');
 await page.goto('/plaza');
 const world=page.frameLocator('iframe[title="ミライフ Unityワールド"]');
 await expect(world.locator('body')).toHaveAttribute('data-unity-state','ready',{timeout:150000});
 await world.locator('body').evaluate(element=>element.dataset.fullscreenContinuity='running');
 await page.getByRole('button',{name:'全画面表示',exact:true}).click();
 await expect(page.getByRole('button',{name:'元に戻す',exact:true})).toHaveAttribute('aria-pressed','true');
 await expect(world.locator('#unity-canvas')).toBeVisible();
 // Let Unity's splash animation finish before capturing the actual world.
 await page.waitForTimeout(4000);
 await page.screenshot({path:'/private/tmp/melife-world-fullscreen.png'});
 await page.getByRole('button',{name:'元に戻す',exact:true}).click();
 await expect(world.locator('body')).toHaveAttribute('data-fullscreen-continuity','running');
 await expect(world.locator('body')).toHaveAttribute('data-unity-state','ready');
});
