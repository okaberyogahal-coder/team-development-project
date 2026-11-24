import {test,expect} from '@playwright/test';
import {selectUser,resetSharedFixture} from './local-demo-fixture';
test.beforeEach(async({page,baseURL})=>{
 const check=await page.request.get('/api/local-demo');
 if(!check.ok())throw new Error(`Fixture endpoint: ${check.status()} ${(await check.text()).slice(0,800)}`);
 await resetSharedFixture(page);await selectUser(page,'photography',baseURL!);
});
test('learns from the sample and completes fixed questions without an AI key',async({page})=>{
 await page.goto('/ai-instructor');
 await expect(page.getByRole('heading',{name:'自然光と背景ぼけの基本',exact:true})).toBeVisible();
 await expect(page.getByText('AIへの接続設定がありません。',{exact:false})).toBeVisible();
 const data=await(await page.request.get('/api/ai-instructor')).json();
 expect(Object.keys(data.questions[0]).sort()).toEqual(['choices','id','prompt']);
 await page.getByLabel('教材本文と質問をOpenAIへ送信することを確認しました').check();
 await page.getByRole('button',{name:'学習を始める',exact:true}).click();
 await expect(page.getByRole('button',{name:'質問する',exact:true})).toBeDisabled();
 for(const [id,choice] of [['light',1],['distance',1],['background',2]] as const){
  const field=page.getByTestId(`quiz-${id}`);
  await field.getByRole('radio').nth(choice).check();
  await field.getByRole('button',{name:'回答する',exact:true}).click();
  await expect(field.getByText('教材を復習する',{exact:true})).toBeVisible();
 }
 await expect(page.getByRole('status').filter({hasText:'確認問題を終了しました'})).toContainText('2 / 3');
 await page.getByTestId('quiz-light').getByRole('button',{name:'教材を復習する'}).click();
 await expect(page.locator('#window-light')).toBeFocused();
 await page.screenshot({path:'/private/tmp/ai-instructor-desktop.png',fullPage:true});
 await page.setViewportSize({width:320,height:740});
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
 await page.screenshot({path:'/private/tmp/ai-instructor-mobile.png',fullPage:true});
});
test('renders AI plain text and navigates to its evidence with a mocked external response',async({page})=>{
 await page.route('**/api/ai-instructor*',async route=>{
  if(route.request().method()==='GET'){
   const r=await route.fetch(),json=await r.json();return route.fulfill({response:r,json:{...json,aiConfigured:true}});
  }
  const body=route.request().postDataJSON();
  if(body.action!=='question')return route.continue();
  // Keep the real owned session; only replace the AI generation boundary for the UI test.
  const state=await(await page.request.get(`/api/ai-instructor?conversationId=${body.conversationId}`)).json();
  const next={...state.conversation,sequence:1,messages:[{role:'user',content:body.question},{role:'assistant',content:'<b>窓辺の光を比べましょう。</b>',sectionIds:['window-light']}]};
  // The client confirms ownership with GET before displaying, so supply that same public view.
  await page.route(`**/api/ai-instructor?conversationId=${body.conversationId}`,r=>r.fulfill({json:{...state,aiConfigured:true,conversation:next}}));
  return route.fulfill({json:next});
 });
 await page.goto('/ai-instructor');await page.getByLabel('教材本文と質問をOpenAIへ送信することを確認しました').check();
 await page.getByRole('button',{name:'学習を始める',exact:true}).click();
 await page.getByLabel('AI講師への質問').fill('光をやわらかくするには？');await page.getByRole('button',{name:'質問する',exact:true}).click();
 const log=page.getByRole('log');await expect(log).toContainText('<b>窓辺の光を比べましょう。</b>');await expect(log.locator('b')).toHaveCount(0);
 await log.getByRole('button',{name:'根拠：1. 窓辺のやわらかい光'}).click();await expect(page.locator('#window-light')).toBeFocused();
});
test('tab revalidation clears a revoked login',async({page,baseURL})=>{
 await page.goto('/ai-instructor');await page.getByLabel('教材本文と質問をOpenAIへ送信することを確認しました').check();
 await page.getByRole('button',{name:'学習を始める',exact:true}).click();
 await expect(page.getByRole('button',{name:'学習を終了する'})).toBeVisible();
 await page.request.post('/api/local-demo',{headers:{Origin:baseURL!},data:{action:'logout'}});
 await page.evaluate(()=>{
  Object.defineProperty(document,'visibilityState',{configurable:true,value:'hidden'});
  document.dispatchEvent(new Event('visibilitychange'));
  Object.defineProperty(document,'visibilityState',{configurable:true,value:'visible'});
  document.dispatchEvent(new Event('visibilitychange'));
  window.dispatchEvent(new Event('focus'));
 });
 await expect(page.getByRole('link',{name:'ローカルログインへ'})).toBeVisible();
 await expect(page.getByRole('log')).toHaveCount(0);
});
test('focus during a delayed AI answer retains the answer and quiz progress',async({page})=>{
 let release!:()=>void;let requested!:()=>void;
 const started=new Promise<void>(r=>{requested=r;});const gate=new Promise<void>(r=>{release=r;});
 let view:Record<string,unknown>|undefined;
 await page.route('**/api/ai-instructor*',async route=>{
  if(route.request().method()==='GET'){
   const r=await route.fetch(),data=await r.json();
   return route.fulfill({response:r,json:{...data,aiConfigured:true,...(view?{conversation:view}:{})}});
  }
  const body=route.request().postDataJSON();
  if(body.action==='question'){
   const data=await(await page.request.get(`/api/ai-instructor?conversationId=${body.conversationId}`)).json();
   requested();await gate;
   view={...data.conversation,sequence:1,messages:[{role:'user',content:body.question},{role:'assistant',content:'やわらかい光を試しましょう。',sectionIds:['window-light']}]};
   return route.fulfill({json:view}).catch(()=>{});
  }
  if(body.action==='answer'&&view){
   if(body.sequence!==view.sequence)return route.fulfill({status:409,json:{error:{code:'STALE',message:'sequence conflict'}}});
   view={...view,sequence:2,results:{light:{correct:true,explanation:'薄いカーテンを通した光です。',sectionId:'window-light'}}};
   return route.fulfill({json:view});
  }
  return route.continue();
 });
 await page.goto('/ai-instructor');await page.getByLabel('教材本文と質問をOpenAIへ送信することを確認しました').check();
 await page.getByRole('button',{name:'学習を始める',exact:true}).click();
 await page.getByLabel('AI講師への質問').fill('光について');await page.getByRole('button',{name:'質問する',exact:true}).click();await started;
 await page.evaluate(()=>window.dispatchEvent(new Event('focus')));
 await expect.poll(async()=>await page.getByRole('button',{name:'学習を終了する'}).count()).toBe(1);
 release();await expect(page.getByRole('log')).toContainText('やわらかい光を試しましょう。');
 const quiz=page.getByTestId('quiz-light');await quiz.getByRole('radio').first().check();await quiz.getByRole('button',{name:'回答する',exact:true}).click();
 await expect(quiz).toContainText('正解です');await expect(page.getByRole('log')).toContainText('やわらかい光を試しましょう。');
});
test('ending study discards a delayed answer and a new start keeps a clean conversation',async({page})=>{
 let release!:()=>void,requested!:()=>void;
 const gate=new Promise<void>(r=>{release=r;}),started=new Promise<void>(r=>{requested=r;});
 await page.route('**/api/ai-instructor*',async route=>{
  if(route.request().method()==='GET'){const r=await route.fetch();return route.fulfill({response:r,json:{...await r.json(),aiConfigured:true}});}
  if(route.request().postDataJSON().action!=='question')return route.continue();
  const body=route.request().postDataJSON();
  const data=await(await page.request.get(`/api/ai-instructor?conversationId=${body.conversationId}`)).json();
  requested();await gate;
  return route.fulfill({json:{...data.conversation,sequence:1,messages:[{role:'assistant',content:'OLD ANSWER',sectionIds:['window-light']}]}}).catch(()=>{});
 });
 await page.goto('/ai-instructor');await page.getByLabel('教材本文と質問をOpenAIへ送信することを確認しました').check();
 await page.getByRole('button',{name:'学習を始める',exact:true}).click();
 await page.getByLabel('AI講師への質問').fill('光について');await page.getByRole('button',{name:'質問する',exact:true}).click();await started;
 await page.getByRole('button',{name:'学習を終了する'}).click();await expect(page.getByRole('button',{name:'学習を始める',exact:true})).toBeVisible();
 release();await page.getByRole('button',{name:'学習を始める',exact:true}).click();
 await expect(page.getByRole('log')).not.toContainText('OLD ANSWER');
 await expect(page.getByTestId('quiz-light').getByRole('button',{name:'回答する',exact:true})).toBeVisible();
});
test('failure recovery preserves questions and suppresses a double submit',async({page})=>{
 let attempts=0;
 await page.route('**/api/ai-instructor*',async route=>{
  if(route.request().method()==='GET'){const r=await route.fetch();return route.fulfill({response:r,json:{...await r.json(),aiConfigured:true}});}
  if(route.request().postDataJSON().action!=='question')return route.continue();
  attempts++;return route.fulfill({status:504,json:{error:{code:'TIMEOUT',message:'AIの回答が時間内に届きませんでした。'}}});
 });
 await page.goto('/ai-instructor');await page.getByLabel('教材本文と質問をOpenAIへ送信することを確認しました').check();
 await page.getByRole('button',{name:'学習を始める',exact:true}).click();await page.getByLabel('AI講師への質問').fill('光について');
 await page.getByRole('button',{name:'質問する',exact:true}).evaluate((button:HTMLButtonElement)=>{button.click();button.click();});
 await expect(page.getByRole('main').getByRole('alert')).toContainText('AIの回答が時間内に届きませんでした。');expect(attempts).toBe(1);
 const quiz=page.getByTestId('quiz-light');await quiz.getByRole('radio').first().focus();await page.keyboard.press('Space');
 await quiz.getByRole('button',{name:'回答する',exact:true}).focus();await page.keyboard.press('Enter');await expect(quiz).toContainText('正解です');
});
test('initial HTML, data and browser scripts omit private grading explanations',async({page})=>{
 const response=await page.goto('/ai-instructor');
 await expect(page.getByRole('heading',{name:'自然光と背景ぼけの基本',exact:true})).toBeVisible();
 const sources=await page.locator('script[src]').evaluateAll(scripts=>scripts.map(script=>(script as HTMLScriptElement).src));
 const artifacts=[await response!.text(),...await Promise.all(sources.map(async src=>(await page.request.get(src)).text()))];
 for(const privateExplanation of ['薄いカーテンを通すと、光と影がやわらかくなりやすくなります。','被写体と背景を離して撮り比べます。ぼけ方は機種や距離でも変わります。','背景を整理したり、撮る位置を変えたりすることで被写体が見やすくなります。']){
  for(const artifact of artifacts)expect(artifact).not.toContain(privateExplanation);
 }
 const data=await(await page.request.get('/api/ai-instructor')).json();
 for(const q of data.questions)expect(Object.keys(q).sort()).toEqual(['choices','id','prompt']);
});
