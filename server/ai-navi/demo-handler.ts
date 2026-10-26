import {AiNaviError,generateSuggestions} from './openai';
import {getLocalDemoRepository,type LocalDemoRepository} from '../local-demo/repository';
import {loadLocalDemoState,buildStoredContext} from '../local-demo/state';
import {getSessionToken,getLocalSessionStore,type LocalSessionStore} from '../local-demo/session';
import {LocalHttpError,exactKeys,guardLocalRequest,readLocalJSON,localJSON} from '../local-demo/http';
export {isLocalDemoUrl} from '../local-demo/http';

const messages:Record<string,string>={
  UNCONFIGURED:'サーバーの OPENAI_API_KEY と OPENAI_MODEL を設定し、再起動してください。',
  TIMEOUT:'提案に時間がかかっています。少し待ってからもう一度お試しください。',
  UPSTREAM:'AIに接続できませんでした。APIの設定や利用状況を確認してください。',
  INVALID_OUTPUT:'提案の形式を確認できませんでした。もう一度お試しください。',
  REFUSED:'今回はAIから提案を取得できませんでした。',
  RATE_LIMIT:'実行回数の上限です。60秒ほど待ってからお試しください。',
  BUSY:'前の提案を処理しています。完了までお待ちください。',
  INVALID:'操作内容を確認してください。',
  UNSELECTED:'ログインしてください。',
  STORE_FAILED:'保存した履歴を読み込めませんでした。JSONと保存先を確認してください。',
  STALE:'履歴が更新されたため、古い提案を破棄しました。最新の履歴でお試しください。',
};
function failure(code:string,status:number):Response{
  return localJSON({error:{code,message:messages[code]??'この操作は利用できません。'}},status);
}
export function createDemoHandler(options:{
  sessions?:LocalSessionStore;repo?:LocalDemoRepository;environment:string;enabled?:boolean;config:{apiKey:string;model:string};generate?:typeof generateSuggestions;now?:()=>number;
}):(request:Request)=>Promise<Response>{
  const repo=options.repo??getLocalDemoRepository();
  const sessions=options.sessions??getLocalSessionStore();
  const generate=options.generate??generateSuggestions;
  const clock=options.now??Date.now;
  let requests:number[]=[];let busy=false;
  return async request=>{
    try{
      guardLocalRequest(request,options.environment,!!options.enabled,true);
      if(request.method!=='POST')throw new LocalHttpError(405,'INVALID');
      if(!exactKeys(await readLocalJSON(request),[]))throw new LocalHttpError(400,'INVALID');
      const token=getSessionToken(request);
      const userId=sessions.resolve(token,clock());
      if(!userId)return failure('UNSELECTED',401);
      if(!options.config.apiKey.trim()||!options.config.model.trim())return failure('UNCONFIGURED',503);
      if(busy)return failure('BUSY',429);
      busy=true;
      try{
        const now=clock();
        const state=await loadLocalDemoState(repo,userId,now);
        if(!state.currentUser)return failure('UNSELECTED',401);
        requests=requests.filter(at=>at>now-60000);
        if(requests.length>=3)return failure('RATE_LIMIT',429);
        requests.push(now);
        let suggestions;
        try{suggestions=await generate(buildStoredContext(state),options.config);}
        catch(error){const code=error instanceof AiNaviError?error.code:'UPSTREAM';return failure(code,code==='TIMEOUT'?504:code==='UNCONFIGURED'?503:502);}
        if(sessions.resolve(token,clock())!==userId)return failure('UNSELECTED',401);
        const latest=await loadLocalDemoState(repo,userId,clock());
        if(sessions.resolve(token,clock())!==userId)return failure('UNSELECTED',401);
        if(state.revision!==latest.revision)return failure('STALE',409);
        return localJSON({suggestions,popularity:state.popularity,revision:state.revision});
      }finally{busy=false;}
    }catch(error){
      if(error instanceof LocalHttpError)return failure(error.code,error.status);
      return failure('STORE_FAILED',500);
    }
  };
}
