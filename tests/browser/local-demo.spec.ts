import {expect,test} from '@playwright/test';
import {readFile} from 'node:fs/promises';
import {join} from 'node:path';
import {resetSharedFixture,selectUser} from './local-demo-fixture';

test.beforeEach(async({page})=>{await resetSharedFixture(page);});

test('passwordless fictional login restores after reload and logout preserves saved history',async({page})=>{
 await page.goto('/login');
 await expect(page.getByRole('button',{name:'そら（架空）で開発用ログイン'})).toBeVisible();
 await page.screenshot({path:'/private/tmp/melife-local-login-desktop.png',fullPage:true});
 await page.setViewportSize({width:390,height:844});
 expect(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth)).toBe(false);
 await page.screenshot({path:'/private/tmp/melife-local-login-mobile.png',fullPage:true});
 await page.setViewportSize({width:1440,height:1000});
 await page.getByRole('button',{name:'そら（架空）で開発用ログイン'}).click();
 await expect(page).toHaveURL(/\/ai-navi$/);
 await expect(page.getByTestId('local-current-user')).toContainText('そら（架空）');
 await page.reload();await expect(page.getByTestId('local-current-user')).toContainText('そら（架空）');
 await page.getByRole('button',{name:'開発用ログアウト'}).click();
 await expect(page.getByRole('button',{name:'そら（架空）で開発用ログイン'})).toBeVisible();
 await expect(page.getByRole('button',{name:'履歴からテーマを見つける'})).toHaveCount(0);
 await page.getByRole('button',{name:'そら（架空）で開発用ログイン'}).click();
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
 await page.getByLabel('開発用ユーザー切替').selectOption('photography');
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
