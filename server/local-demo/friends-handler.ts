import {isLocalUserId} from '../../shared/local-demo';
import type {LocalDemoFriendsRepository} from './repository';
import {LocalStoreError} from './repository';
import {getSessionToken,type LocalSessionStore} from './session';
import {LocalHttpError,exactKeys,guardLocalRequest,localJSON,readLocalJSON} from './http';

type Options={
  repo:LocalDemoFriendsRepository;
  sessions:LocalSessionStore;
  environment:string;
  enabled:boolean;
  now?:()=>number;
};

function failure(message:string,status:number){return localJSON({message},status);}

export function createLocalFriendsHandler(options:Options){
  const now=options.now??Date.now;
  return async(request:Request)=>{
    try{
      guardLocalRequest(request,options.environment,options.enabled,request.method!=='GET');
      if(!['GET','POST','PATCH','DELETE'].includes(request.method))throw new LocalHttpError(405,'INVALID');
      const token=getSessionToken(request);
      const userId=options.sessions.resolve(token,now());
      if(!userId)return failure('モックアカウントでログインしてください。',401);

      if(request.method==='GET'){
        const nickname=new URL(request.url).searchParams.get('nickname')?.trim()??'';
        if(nickname&&(nickname.length<2||nickname.length>40||/[\u0000-\u001f\u007f]/.test(nickname))) {
          return failure('ニックネームは2〜40文字で入力してください。',400);
        }
        return localJSON(await options.repo.readFriendPage(userId,nickname||undefined));
      }

      const body=await readLocalJSON(request);
      if(options.sessions.resolve(token,now())!==userId)return failure('モックアカウントでログインしてください。',401);
      if(request.method==='POST'){
        if(!exactKeys(body,['targetId'])||!isLocalUserId(body.targetId))return failure('申請する利用者を選び直してください。',400);
        if(body.targetId===userId)return failure('自分自身には申請できません。',400);
        await options.repo.requestFriend(userId,body.targetId,new Date(now()));
        return localJSON({message:'フレンド申請を送りました。'});
      }
      if(request.method==='PATCH'){
        if(!exactKeys(body,['targetId','action'])||!isLocalUserId(body.targetId)||
          (body.action!=='accept'&&body.action!=='decline')||body.targetId===userId) {
          return failure('申請内容を確認してください。',400);
        }
        await options.repo.respondToFriendRequest(userId,body.targetId,body.action,new Date(now()));
        return localJSON({message:body.action==='accept'?'フレンドになりました。':'申請を見送りました。'});
      }
      if(!exactKeys(body,['targetId'])||!isLocalUserId(body.targetId)||body.targetId===userId) {
        return failure('対象を選び直してください。',400);
      }
      await options.repo.removeFriend(userId,body.targetId);
      return localJSON({message:'フレンドまたは申請を削除しました。'});
    }catch(error){
      if(error instanceof LocalHttpError){
        return failure(error.code==='NOT_FOUND'?'ページが見つかりません。':error.code==='FORBIDDEN'?'この画面からもう一度操作してください。':'操作内容を確認してください。',error.status);
      }
      if(error instanceof LocalStoreError&&error.code==='INVALID_INPUT'){
        return failure('申請またはフレンドの状態が変わっています。画面を更新してください。',409);
      }
      return failure('モックのフレンド情報を読み書きできませんでした。保存ファイルを確認してください。',500);
    }
  };
}
