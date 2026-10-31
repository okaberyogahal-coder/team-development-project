import {expect,test} from '@playwright/test';
import {readFileSync} from 'node:fs';
const sessionScript=readFileSync('unity/FutureCity/Assets/WebGLTemplates/Melife/session.js','utf8');
const fixture=`<!doctype html><style>.host-session-header #session-bar{display:none}</style><div id="session-bar"><img id="session-avatar" hidden><span id="session-status"></span><button id="session-retry" hidden>再確認</button><a id="session-account">ログインする</a><button id="session-logout" hidden>ログアウト</button><p id="session-error" hidden></p></div><canvas id="unity-canvas" tabindex="0"></canvas><script>${sessionScript}</script><script>
window.melifeUnityReady({SendMessage:function(o,m,json){if(m!=='ApplySession')return;var data=JSON.parse(json);melifeSessionApplied(JSON.stringify({revision:data.revision,authenticated:data.authenticated,state:data.status,nickname:data.profile?.nickname||'',avatarId:data.profile?.avatarId||'',avatarLabel:'ねこ'}));}});melifeBridgeReady();parent.postMessage({type:'melife:ready'},location.origin);
</script>`;
test('logo and equally sized controls form one row with frequent actions at the right',async({page})=>{
 let signedIn=true,logouts=0;
 await page.route('**/api/unity/session',route=>route.fulfill({json:signedIn?{authenticated:true,profile:{userId:'11111111-1111-4111-8111-111111111111',nickname:'そら',avatarId:'cat',teaches:[],wantsToLearn:[]}}:{authenticated:false,profile:null}}));
 await page.route('**/api/auth/sign-out',route=>{signedIn=false;logouts++;return route.fulfill({json:{success:true}});});
 await page.route('**/unity/FutureCity/index.html*',route=>route.fulfill({contentType:'text/html',body:fixture}));
 await page.goto('/plaza');
 const toolbar=page.locator('.world-viewer-toolbar');
 await expect(toolbar.getByRole('link',{name:'ミライフ トップへ'}).locator('img')).toBeVisible();
 await expect(toolbar.getByText('そらさんで参加中')).toBeVisible();
 const controls=[toolbar.getByRole('button',{name:'ログアウト',exact:true}),toolbar.getByRole('link',{name:'TOPへ戻る',exact:true}),toolbar.getByRole('link',{name:'マイページ',exact:true}),toolbar.getByRole('button',{name:'全画面表示',exact:true})];
 const rects=await Promise.all(controls.map(c=>c.boundingBox()));
 for(const r of rects){expect(r).not.toBeNull();expect(r!.y).toBeCloseTo(rects[0]!.y,0);expect(r!.width).toBeCloseTo(rects[0]!.width,0);expect(r!.height).toBeCloseTo(rects[0]!.height,0);}
 for(let i=1;i<rects.length;i++)expect(rects[i]!.x).toBeGreaterThan(rects[i-1]!.x);
 await expect(page.frameLocator('iframe').locator('#session-bar')).toBeHidden();
 await page.screenshot({path:'/private/tmp/melife-world-header.png'});
 await controls[0].click();await expect(toolbar.getByRole('link',{name:'ログインする',exact:true})).toBeVisible();expect(logouts).toBe(1);
});
test('untrusted host messages do not change the displayed identity',async({page})=>{
 await page.route('**/unity/FutureCity/index.html*',route=>route.fulfill({contentType:'text/html',body:'<!doctype html>'}));
 await page.goto('/plaza');
 await page.evaluate(()=>window.postMessage({type:'melife:session-header',authenticated:true,status:'偽の利用者'},location.origin));
 await expect(page.locator('.world-viewer-toolbar')).not.toContainText('偽の利用者');
});

test('logout failures and session recovery remain available in the unified header',async({page})=>{
 let unavailable=false;
 await page.route('**/api/unity/session',route=>unavailable?route.fulfill({status:503,json:{}}):route.fulfill({json:{authenticated:true,profile:{userId:'11111111-1111-4111-8111-111111111111',nickname:'そら',avatarId:'cat',teaches:[],wantsToLearn:[]}}}));
 await page.route('**/api/auth/sign-out',route=>route.fulfill({status:503,json:{}}));
 await page.route('**/unity/FutureCity/index.html*',route=>route.fulfill({contentType:'text/html',body:fixture}));
 await page.goto('/plaza');const toolbar=page.locator('.world-viewer-toolbar');
 await toolbar.getByRole('button',{name:'ログアウト',exact:true}).click();
 await expect(page.locator('.world-session-error')).toContainText('ログアウトできませんでした');
 await expect(toolbar.getByRole('button',{name:'ログアウト',exact:true})).toBeEnabled();
 unavailable=true;
 await page.frameLocator('iframe').locator('body').evaluate(()=>window.dispatchEvent(new Event('focus')));
 await expect(page.locator('.world-session-error')).toContainText('ログイン状態を確認できません');
 await expect(toolbar.getByRole('button',{name:'ログアウト',exact:true})).toHaveCount(0);
 unavailable=false;await page.getByRole('button',{name:'再確認',exact:true}).click();
 await expect(toolbar.getByText('そらさんで参加中')).toBeVisible();await expect(page.locator('.world-session-error')).toHaveCount(0);
});
test('narrow windows keep the header on one row without page overflow',async({page})=>{
 await page.setViewportSize({width:390,height:844});
 await page.route('**/api/unity/session',route=>route.fulfill({json:{authenticated:true,profile:{userId:'11111111-1111-4111-8111-111111111111',nickname:'長い名前の利用者',avatarId:'cat',teaches:[],wantsToLearn:[]}}}));
 await page.route('**/unity/FutureCity/index.html*',route=>route.fulfill({contentType:'text/html',body:fixture}));
 await page.goto('/plaza');const toolbar=page.locator('.world-viewer-toolbar');await expect(toolbar.getByRole('button',{name:'ログアウト',exact:true})).toBeAttached();
 const boxes=await toolbar.locator('.world-header-control').evaluateAll(nodes=>nodes.map(n=>({y:n.getBoundingClientRect().y,height:n.getBoundingClientRect().height})));
 expect(boxes).toHaveLength(4);for(const box of boxes)expect(box.y).toBeCloseTo(boxes[0].y,0);
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth&&document.documentElement.scrollHeight<=innerHeight)).toBe(true);
});
