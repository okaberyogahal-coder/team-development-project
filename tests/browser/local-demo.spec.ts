import {expect,test} from '@playwright/test';
import {readFile} from 'node:fs/promises';
import {join} from 'node:path';
import {DEFAULT_AVATAR,DEMO_PROFILE_KEY} from '../../shared/demo-profile';
import {resetSharedFixture,selectUser} from './local-demo-fixture';

test.beforeEach(async({page})=>{await resetSharedFixture(page);});

async function formLogin(page:import('@playwright/test').Page,email='sora@example.test'){
 await expect(page.getByRole('button',{name:'ログイン',exact:true})).toBeEnabled();
 await page.getByLabel('メールアドレス',{exact:true}).fill(email);
 await page.getByLabel('パスワード',{exact:true}).fill('MilifeDemo2026!');
 await page.getByRole('button',{name:'ログイン',exact:true}).click();
}
test('existing email and password form logs in, restores and logs out without selector buttons',async({page})=>{
 await page.goto('/login');
 await expect(page.getByRole('button',{name:/で開発用ログイン/})).toHaveCount(0);
 await page.getByLabel('メールアドレス',{exact:true}).fill('sora@example.test');
 await page.getByLabel('パスワード',{exact:true}).fill('wrong');
 await page.getByRole('button',{name:'ログイン',exact:true}).click();
 await expect(page.locator('form').getByRole('alert')).toContainText('メールアドレスまたはパスワード');
 await expect(page.getByLabel('パスワード',{exact:true})).toHaveValue('');
 await page.screenshot({path:'/private/tmp/melife-local-login-desktop.png',fullPage:true});
 await page.setViewportSize({width:390,height:844});
 expect(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth)).toBe(false);
 await page.screenshot({path:'/private/tmp/melife-local-login-mobile.png',fullPage:true});
 await page.setViewportSize({width:1440,height:1000});
 await page.evaluate(({key,appearance})=>localStorage.setItem(key,JSON.stringify({version:1,id:'another-user',nickname:'別の人',appearance})),{key:DEMO_PROFILE_KEY,appearance:DEFAULT_AVATAR});
 await formLogin(page);
 await expect(page).toHaveURL(/\/avatar$/);
 await expect(page.getByTitle('3Dアバター作成')).toBeVisible();
 await expect(page.locator('.unity-onboarding')).toContainText('そら（架空）');
 const profile=await page.evaluate(key=>JSON.parse(localStorage.getItem(key)!),DEMO_PROFILE_KEY);
 expect(profile.id).toBe('photography');expect(profile.appearance).toBeNull();
 await page.evaluate(({key,appearance})=>{const profile=JSON.parse(localStorage.getItem(key)!);profile.appearance=appearance;localStorage.setItem(key,JSON.stringify(profile));},{key:DEMO_PROFILE_KEY,appearance:DEFAULT_AVATAR});
 await page.goto('/ai-navi');
 await expect(page.getByTestId('local-current-user')).toContainText('そら（架空）');
 await expect(page.getByLabel('開発用ユーザー切替')).toHaveCount(0);
 const storage=await page.evaluate(()=>JSON.stringify({...localStorage,...sessionStorage}));expect(storage).not.toContain('MilifeDemo2026!');
 await page.reload();await expect(page.getByTestId('local-current-user')).toContainText('そら（架空）');
 await page.getByRole('button',{name:'ログアウト',exact:true}).click();
 await expect(page.getByRole('link',{name:'ログイン画面へ'})).toBeVisible();
 await expect(page.getByRole('button',{name:'履歴からテーマを見つける'})).toHaveCount(0);
 await page.getByRole('link',{name:'ログイン画面へ'}).click();await formLogin(page);
 await expect(page).toHaveURL(/\/avatar$/);
 expect(await page.evaluate(key=>JSON.parse(localStorage.getItem(key)!).appearance,DEMO_PROFILE_KEY)).toEqual(DEFAULT_AVATAR);
 await page.goto('/ai-navi');
 await expect(page.getByTestId('learning-history')).toContainText('自然光と背景ぼけの基本');
});

test('viewing and explicit completion are persisted to the selected JSON and never to another user',async({page,baseURL})=>{
 await selectUser(page,'newcomer',baseURL!);await page.goto('/ai-navi');
 await expect(page.getByTestId('learning-history')).toContainText('まだ履歴がありません');
 await page.getByText('サンプル教材を開いて履歴を記録').click();
 await page.getByRole('button',{name:'スマートフォンで写真を整理するを開く'}).click();
 await expect(page.getByTestId('learning-history')).toContainText('閲覧');
 let saved=JSON.parse(await readFile(join(process.env.MILIFE_LOCAL_DEMO_TEST_DIR!,'users/newcomer.json'),'utf8'));
 expect(saved.events.map((x:any)=>x.kind)).toEqual(['view']);
 await page.getByRole('button',{name:'受講完了を記録'}).click();
 await expect(page.getByTestId('learning-history')).toContainText('受講完了');
 saved=JSON.parse(await readFile(join(process.env.MILIFE_LOCAL_DEMO_TEST_DIR!,'users/newcomer.json'),'utf8'));
 expect(saved.events.map((x:any)=>x.kind)).toEqual(['view','complete']);
 await page.reload();await expect(page.getByTestId('learning-history')).toContainText('受講完了');
 await selectUser(page,'photography',baseURL!);await page.reload();
 await expect(page.getByTestId('local-current-user')).toContainText('そら（架空）');
 await expect(page.getByTestId('learning-history')).not.toContainText('スマートフォンで写真を整理する');
 const other=JSON.parse(await readFile(join(process.env.MILIFE_LOCAL_DEMO_TEST_DIR!,'users/photography.json'),'utf8'));
 expect(other.events).toHaveLength(2);
});

test('fixture setup refuses a server using a different store before any writes',async()=>{
 const {resetSharedFixture}=await import('./local-demo-fixture');
 const wrongServer={request:{get:async()=>({ok:()=>true,headers:()=>({'x-melife-local-store':'shared'})})}};
 await expect(resetSharedFixture(wrongServer as never)).rejects.toThrow(/保存先/);
});
