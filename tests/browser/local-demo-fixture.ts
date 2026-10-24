import {copyFile} from 'node:fs/promises';
import {resolve,join} from 'node:path';
import type {Page} from '@playwright/test';
export async function resetSharedFixture(){
 const root=process.env.MILIFE_LOCAL_DEMO_TEST_DIR;
 if(!root||!resolve(root).startsWith('/private/tmp/')||resolve(root)===resolve('data/local-demo')) throw new Error('Use a dedicated temporary JSON copy via MILIFE_LOCAL_DEMO_TEST_DIR');
 for(const id of ['photography','food-photo','newcomer']) await copyFile(join('data/local-demo/users',`${id}.json`),join(root,'users',`${id}.json`));
}
export async function selectUser(page:Page,id:string,baseURL:string){
 const response=await page.request.post('/api/local-demo',{headers:{Origin:baseURL},data:{action:'login',userId:id}});
 if(!response.ok()) throw new Error(`Cannot select local fixture user (${response.status()})`);
}
