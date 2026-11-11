import {test,expect} from '@playwright/test';

test('legacy editor requires gender and adult age and submits both fields',async({page})=>{
 let payload:Record<string,unknown>|undefined;
 await page.route('**/api/account/profile',async route=>{
  if(route.request().method()==='PATCH'){
   payload=route.request().postDataJSON();
   await route.fulfill({status:503,json:{message:'保存を再試行してください。'}});return;
  }
  await route.fulfill({json:{ownerId:'existing-user',nickname:'学び仲間',gender:null,age:null,interests:[],teaches:[],wantsToLearn:[],strengths:'',goal:'',profileVersion:1,preferencesVersion:1}});
 });
 await page.goto('/account/edit');
 const gender=page.getByLabel('性別（必須）');const age=page.getByLabel('年齢（必須・本人のみ）');
 await expect(gender).toHaveValue('');await expect(age).toHaveValue('');
 await expect(gender).toHaveAttribute('required','');await expect(age).toHaveAttribute('min','18');
 await gender.selectOption('female');await age.fill('17');
 await page.getByRole('button',{name:'変更を保存',exact:true}).click();expect(payload).toBeUndefined();
 await age.fill('59');await expect(page.getByRole('status')).toContainText('ミドルシニア');
 await page.getByRole('button',{name:'変更を保存',exact:true}).click();
 await expect(page.getByRole('alert').filter({hasText:'保存を再試行してください。'})).toBeVisible();
 expect(payload).toMatchObject({gender:'female',age:59,profileVersion:1,preferencesVersion:1});
 await expect(gender).toHaveValue('female');await expect(age).toHaveValue('59');
 await gender.scrollIntoViewIfNeeded();
 await page.screenshot({path:'/private/tmp/melife-demographics-profile.png',fullPage:true});
});
