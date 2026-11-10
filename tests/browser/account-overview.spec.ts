import {test,expect,type Page} from '@playwright/test';
import {randomUUID} from 'node:crypto';
import {DEFAULT_AVATAR,DEMO_PROFILE_KEY} from '../../shared/demo-profile';

const fixturePath=process.env.ACCOUNT_OVERVIEW_FIXTURE;
const emails:string[]=[];
const profile={nickname:'花と写真',gender:'male',age:35,avatar:'cat',interests:['写真','料理、栄養'],wantsToLearn:['園芸','英会話'],teaches:['カメラ'],strengths:'人の話を聞くこと\n写真を撮ること',goal:'毎月、新しいことに挑戦する。'};
const appearance={...DEFAULT_AVATAR,parts:[1,0,2,1,1,0,2,0,0],colors:[1,2,5,7,2,6,0,0,6]};

async function open(page:Page,options:{saved?:boolean;foreign?:boolean;gender?:string}={saved:true}){
 let ownerId='account-preview-fixture';
 if(!fixturePath){
  const {authTestDatabaseUrl}=await import('../support/auth-database');
  authTestDatabaseUrl(); // Never run write tests on the shared development database.
  const email=`account-overview-${randomUUID()}@example.invalid`;emails.push(email);
  const password='account-overview-password-123';
  const response=await page.request.post('/api/account/signup',{headers:{Origin:new URL(test.info().project.use.baseURL!).origin},data:{...profile,gender:options.gender||profile.gender,email,password,confirmation:password,adult:true,terms:true,privacy:true}});
  expect(response.ok()).toBe(true);
  const {ownerId:id}=await (await page.request.get('/api/account/profile')).json();ownerId=id;
 }
 if(options.saved)await page.addInitScript(({key,id,appearance})=>localStorage.setItem(key,JSON.stringify({version:1,id,nickname:'古い名前',appearance})),{key:DEMO_PROFILE_KEY,id:options.foreign?'another-owner':ownerId,appearance});
 await page.goto(fixturePath?`${fixturePath}${options.gender?`?gender=${options.gender}`:''}`:'/account?updated=1');
 return ownerId;
}

test.afterAll(async()=>{
 if(!emails.length)return;
 const {createDatabase}=await import('../../server/db/connection');
 const {authTestDatabaseUrl}=await import('../support/auth-database');
 const db=createDatabase(authTestDatabaseUrl());
 try{await db.user.deleteMany({where:{email:{in:emails}}});}finally{await db.$disconnect();}
});

const previewHTML=`<!doctype html><script>window.addEventListener('message',event=>{if(event.data?.type==='melife:initialize'){window.receivedProfile=event.data;parent.postMessage({type:'melife:profile-ready'},location.origin)}});parent.postMessage({type:'melife:ready'},location.origin);</script>`;
test('unauthenticated account access still returns to login',async({page})=>{
 await page.goto('/account');
 await expect(page).toHaveURL(/\/login$/);
});
for(const {width,height} of [{width:1440,height:900},{width:1366,height:768},{width:1280,height:800},{width:1024,height:600}])test(`desktop ${width}x${height} fits without page scrolling and preserves 30:70 columns`,async({page})=>{
 await page.setViewportSize({width,height});
 await page.route('**/unity/FutureCity/index.html*',route=>route.fulfill({contentType:'text/html',body:previewHTML}));
 const ownerId=await open(page);
 await expect(page.getByRole('heading',{name:'こんにちは、花と写真さん。'})).toBeVisible();
 const left=(await page.getByRole('complementary',{name:'あなたのアバター'}).boundingBox())!;
 const right=(await page.getByRole('region',{name:'あなたのプロフィール',exact:true}).boundingBox())!;
 expect(left.width/(left.width+right.width)).toBeCloseTo(.3,2);expect(right.x).toBeGreaterThan(left.x+left.width);
 for(const value of ['写真','料理、栄養','園芸','英会話','カメラ',profile.strengths,profile.goal])await expect(page.locator('.account-profile')).toContainText(value);
 for(const [name,href] of [['アバターを変更','/avatar'],['プロフィールを編集','/account/edit'],['ワールドに入る','/plaza'],['トップページへ','/']])await expect(page.getByRole('link',{name:new RegExp(name)})).toHaveAttribute('href',href);
 await expect(page.getByRole('button',{name:'ログアウト',exact:true})).toBeVisible();
 await expect(page.locator('.account-avatar-stage')).toHaveAttribute('aria-busy','false');
 const received=await page.frameLocator('iframe[title="現在の3Dアバター"]').locator('body').evaluate(()=> (window as unknown as {receivedProfile:unknown}).receivedProfile);
 expect(received).toEqual({type:'melife:initialize',mode:'profile',profile:{version:1,id:ownerId,nickname:profile.nickname,appearance,gender:'male'}});
 expect(await page.evaluate(key=>JSON.parse(localStorage.getItem(key)!).nickname,DEMO_PROFILE_KEY)).toBe('古い名前');
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
 expect(await page.evaluate(()=>document.documentElement.scrollHeight<=innerHeight)).toBe(true);
 expect(await page.locator('.account-profile').evaluate(element=>[element,...element.querySelectorAll('*')].filter(node=>/auto|scroll/.test(getComputedStyle(node).overflowY)&&node.scrollHeight>node.clientHeight+1).map(node=>node.className))).toEqual([]);
 for(const control of [page.getByRole('link',{name:/アバターを変更/}),page.getByRole('link',{name:/プロフィールを編集/}),page.getByRole('button',{name:'ログアウト',exact:true})]){
  const bounds=(await control.boundingBox())!;expect(bounds.y).toBeGreaterThanOrEqual(0);expect(bounds.y+bounds.height).toBeLessThanOrEqual(height);
 }
 await page.screenshot({path:test.info().outputPath(`account-${width}.png`),fullPage:true});
});

for(const foreign of [false,true])test(`${foreign?'foreign':'missing'} wardrobe is not displayed and profile remains readable`,async({page})=>{
 await open(page,{saved:foreign,foreign});
 await expect(page.getByText('あなたらしい姿を、ここに。')).toBeVisible();
 await expect(page.getByTitle('現在の3Dアバター')).toHaveCount(0);
 await expect(page.getByRole('heading',{name:'基本情報',exact:true})).toBeVisible();
});

test('small desktop keeps controls visible with a long nickname and both notices',async({page})=>{
 await page.setViewportSize({width:1024,height:600});
 await open(page,{saved:false});
 await page.locator('.account-page-heading h1').evaluate(node=>{node.textContent=`こんにちは、${'あ'.repeat(20)}さん。`;});
 await page.locator('.account-avatar-identity h2').evaluate(node=>{node.textContent='あ'.repeat(20);});
 await page.locator('.account-grid').evaluate(grid=>{
  const warning=document.createElement('p');warning.className='account-notice account-notice-warning';warning.setAttribute('role','status');warning.textContent='性別・年齢の登録が必要です。プロフィールを編集してください。';grid.before(warning);
 });
 for(const control of [page.getByRole('link',{name:/アバターを変更/}),page.getByRole('button',{name:'ログアウト',exact:true})]){
  const box=(await control.boundingBox())!;expect(box.y+box.height).toBeLessThanOrEqual(600);
 }
 expect(await page.locator('.account-profile').evaluate(element=>element.scrollHeight<=element.clientHeight)).toBe(true);
});

test('long email, topics and private text wrap inside the desktop columns',async({page})=>{
 await page.setViewportSize({width:1280,height:800});
 await open(page,{saved:false});
 // Stress the actual rendered cards with maximum-length content, independently
 // of API validation and without writing synthetic personal data to the DB.
 await page.locator('.account-email-row dd').evaluate(node=>{node.textContent='a'.repeat(238)+'@example.invalid';});
 await page.locator('.account-topics > span').evaluateAll(nodes=>nodes.forEach(node=>{node.textContent='長い分野名'.repeat(6);}));
 await page.locator('.account-private-details dd').evaluateAll(nodes=>nodes.forEach(node=>{node.textContent=('改行のある目標です。\n').repeat(40);}));
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
 const right=(await page.locator('.account-profile').boundingBox())!;
 for(const card of await page.locator('.account-info-card').all()){
  const box=(await card.boundingBox())!;expect(box.x).toBeGreaterThanOrEqual(right.x);expect(box.x+box.width).toBeLessThanOrEqual(right.x+right.width+1);
 }
});

test('preview ignores messages from outside the iframe and recovers from a load error',async({page})=>{
 let loads=0;
 await page.route('**/unity/FutureCity/index.html*',route=>route.fulfill({contentType:'text/html',body:++loads===1?'<!doctype html><p>Waiting</p>':previewHTML}));
 await open(page);
 await page.evaluate(()=>window.postMessage({type:'melife:profile-ready'},location.origin));
 await expect(page.getByText('アバターを読み込み中',{exact:true})).toBeVisible();
 await page.frameLocator('iframe[title="現在の3Dアバター"]').locator('body').evaluate(()=>parent.postMessage({type:'melife:error'},location.origin));
 await expect(page.getByText('アバターを表示できませんでした。')).toBeVisible();
 await expect(page.getByRole('heading',{name:'あなたのプロフィール',exact:true})).toBeVisible();
 await page.getByRole('button',{name:'もう一度読み込む ↻'}).click();
 await expect(page.locator('.account-avatar-stage')).toHaveAttribute('aria-busy','false');
 await expect(page.getByText('アバターを表示できませんでした。')).toHaveCount(0);
});

test('real profile template forwards server gender and appearance to Unity',async({page})=>{
 await page.route('**/unity/FutureCity/Build/*.loader.js',route=>route.fulfill({contentType:'text/javascript',body:`window.createUnityInstance=async()=>({SendMessage:(object,method,json)=>{if(method==='Initialize')window.initializedProfile=JSON.parse(json)}});`}));
 await open(page);
 const frame=page.frameLocator('iframe[title="現在の3Dアバター"]');
 await expect(page.locator('.account-avatar-stage')).toHaveAttribute('aria-busy','false');
 const received=await frame.locator('body').evaluate(()=>(window as unknown as {initializedProfile:unknown}).initializedProfile);
 expect(received).toMatchObject({mode:'profile',gender:'male',appearance});
});

test('real WebGL account preview renders without world-session navigation or wardrobe writes',async({page})=>{
 test.skip(process.env.MELIFE_ACCOUNT_WEBGL!=='1','Opt in after building Unity WebGL.');
 test.setTimeout(180000);
 await page.setViewportSize({width:1366,height:768});
 let sessionRequests=0;
 await page.route('**/api/unity/session',route=>{sessionRequests++;return route.fulfill({status:503,json:{message:'Session unavailable'}});});
 await open(page);
 const world=page.frameLocator('iframe[title="現在の3Dアバター"]');
 await expect(world.locator('body')).toHaveAttribute('data-unity-state','ready',{timeout:150000});
 await expect(page.locator('.account-avatar-stage')).toHaveAttribute('aria-busy','false',{timeout:150000});
 await expect.poll(()=>world.locator('canvas').evaluate(canvas=>new Promise<number>(resolve=>requestAnimationFrame(()=>{
  const c=canvas as HTMLCanvasElement, gl=c.getContext('webgl2')!;
  const pixels=new Uint8Array(c.width*c.height*4);gl.readPixels(0,0,c.width,c.height,gl.RGBA,gl.UNSIGNED_BYTE,pixels);
  let redShirt=0,sky=0;
  for(let i=0;i<pixels.length;i+=4){
   if(pixels[i]>150&&pixels[i]>pixels[i+1]*1.4&&pixels[i]>pixels[i+2]*1.2)redShirt++;
   if(pixels[i]>220&&pixels[i+1]>235&&pixels[i+2]>245)sky++;
  }
  // Both the sky backdrop and the selected red shirt must render: a splash
  // screen or the ordinary world UI is not an initialized avatar preview.
  const corners=[[10,10],[c.width-10,10],[10,c.height-10],[c.width-10,c.height-10]];
  const clearBackdrop=corners.every(([x,y])=>{const i=(y*c.width+x)*4;return pixels[i]>=234&&pixels[i+1]>=245&&pixels[i+2]>=250;});
  resolve(clearBackdrop&&sky>c.width*c.height*.6?redShirt:0);
 }))),{timeout:30000}).toBeGreaterThan(500);
 expect(sessionRequests).toBe(0);
 expect(page.url()).toContain(fixturePath||'/account');
 expect(await page.evaluate(key=>JSON.parse(localStorage.getItem(key)!).appearance,DEMO_PROFILE_KEY)).toEqual(appearance);
 await page.screenshot({path:test.info().outputPath('account-real-webgl.png'),fullPage:true});
});
