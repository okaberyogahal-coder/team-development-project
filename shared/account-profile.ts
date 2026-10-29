import {AuthInputError} from './auth/validation';
export type ProfileFields={nickname:string;interests:string[];wantsToLearn:string[];teaches:string[];strengths:string;goal:string};
export type AccountProfile=ProfileFields&{profileVersion:number;preferencesVersion:number};
export function profileEditInput(value:unknown):AccountProfile{
 if(!value||typeof value!=='object'||Array.isArray(value))throw new AuthInputError('入力内容を確認してください。');
 const data=value as Record<string,unknown>;
 const fields=['nickname','interests','wantsToLearn','teaches','strengths','goal','profileVersion','preferencesVersion'];
 if(Object.keys(data).some(key=>!fields.includes(key)))throw new AuthInputError('編集できない項目が含まれています。');
 function text(key:string,max:number,required=false){
  const v=data[key];
  if(typeof v!=='string'||v.trim().length>max||(required&&!v.trim())||v.includes('\0'))throw new AuthInputError('入力項目の文字数・内容を確認してください。');
  return v.trim();
 }
 function tags(key:string){
  const v=data[key];
  if(!Array.isArray(v)||v.length>10||v.some(x=>typeof x!=='string'||!x.trim()||x.trim().length>30||x.includes('\0')))throw new AuthInputError('各分野は1項目30文字・10個以内で入力してください。');
  return [...new Set((v as string[]).map(x=>x.trim()))];
 }
 function version(key:string){const v=data[key];if(typeof v!=='number'||!Number.isInteger(v)||v<0||v>=2147483647)throw new AuthInputError('最新のプロフィールを読み込み直してください。');return v;}
 return {nickname:text('nickname',20,true),interests:tags('interests'),wantsToLearn:tags('wantsToLearn'),teaches:tags('teaches'),strengths:text('strengths',500),goal:text('goal',500),profileVersion:version('profileVersion'),preferencesVersion:version('preferencesVersion')};
}
