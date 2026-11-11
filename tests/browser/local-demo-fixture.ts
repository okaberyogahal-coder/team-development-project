import {createHash} from 'node:crypto';
import {copyFile} from 'node:fs/promises';
import {resolve,join} from 'node:path';
import type {Page} from '@playwright/test';
export async function resetSharedFixture(page:Page){
 const root=process.env.MILIFE_LOCAL_DEMO_TEST_DIR;
 if(!root||!resolve(root).startsWith('/private/tmp/')||resolve(root)===resolve('data/local-demo')) throw new Error('Use a dedicated temporary JSON copy via MILIFE_LOCAL_DEMO_TEST_DIR');
 const response=await page.request.get('/api/local-demo');
 const expected=createHash('sha256').update(resolve(root)).digest('hex');
 if(!response.ok()||response.headers()['x-melife-local-store']!==expected)throw new Error('試験サーバーの保存先が一致しません。');
 for(const id of ['photography','food-photo','newcomer']) await copyFile(join('tests/fixtures/local-demo/users',`${id}.json`),join(root,'users',`${id}.json`));
}
export async function selectUser(page:Page,id:string,baseURL:string){
 const root=process.env.MILIFE_LOCAL_DEMO_TEST_DIR;
 const check=await page.request.get('/api/local-demo');
 if(!root||!check.ok()||check.headers()['x-melife-local-store']!==createHash('sha256').update(resolve(root)).digest('hex'))throw new Error('試験サーバーの保存先が一致しません。');
 const response=await page.request.post('/api/local-demo',{headers:{Origin:baseURL},data:{action:'login',email:({photography:'sora', 'food-photo':'aoi', newcomer:'haru'} as Record<string,string>)[id]+'@example.test',password:'MilifeDemo2026!'}});
 if(!response.ok()) throw new Error(`Cannot select local fixture user (${response.status()})`);
}
