import {AuthInputError} from '../../shared/auth/validation';
import {profileEditInput,type AccountProfile} from '../../shared/account-profile';
import {authJson,checkOrigin,readAuthJson} from '../auth/http';
export class ProfileConflictError extends Error{}
export class ProfileAccessError extends Error{}
type Dependencies={getUser:()=>Promise<{id:string}|null>;read:(id:string)=>Promise<unknown>;save:(id:string,input:AccountProfile)=>Promise<unknown>;allowSave:(id:string)=>Promise<boolean>};
export function createProfileHandlers(deps:Dependencies){
 return {
  async GET(){
   try{
    const user=await deps.getUser();
    if(!user)return authJson({message:'ログインしてください。'},401);
    return authJson(await deps.read(user.id),200,{Vary:'Cookie','Cross-Origin-Resource-Policy':'same-origin'});
   }catch{return authJson({message:'プロフィールを読み込めませんでした。もう一度お試しください。'},503);}
  },
  async PATCH(request:Request){
   if(!checkOrigin(request))return authJson({message:'この画面からもう一度操作してください。'},403);
   try{
    const user=await deps.getUser();
    if(!user)return authJson({message:'ログインしてください。'},401);
    if(request.headers.get("x-profile-owner")!==user.id)return authJson({message:"ログイン中の利用者が変わりました。入力内容を控え、最新情報を読み込んでください。"},409);
    let input:AccountProfile;
    try{input=profileEditInput(await readAuthJson(request));}
    catch(error){return authJson({message:error instanceof AuthInputError?error.message:'入力内容を確認してください。'},400);}
    if(!await deps.allowSave(user.id))return authJson({message:'少し待ってから保存してください。'},429,{'Retry-After':'60'});
    return authJson(await deps.save(user.id,input));
   }catch(error){
    if(error instanceof ProfileAccessError)return authJson({message:'ログイン状態を確認してください。'},401);
    if(error instanceof ProfileConflictError)return authJson({message:'別の画面で更新されています。入力内容を控え、最新情報を読み込んでください。'},409);
    return authJson({message:'保存できませんでした。入力内容は残しています。もう一度お試しください。'},503);
   }
  }
 };
}
