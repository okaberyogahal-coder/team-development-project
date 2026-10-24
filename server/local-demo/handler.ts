import {isLocalUserId} from '../../shared/local-demo';
import type {LocalDemoRepository} from './repository';
import {LocalStoreError} from './repository';
import {loadLocalDemoState} from './state';
import {getSelectedLocalUser,selectionCookie} from './session';
import {LocalHttpError,exactKeys,guardLocalRequest,localFailure,localJSON,readLocalJSON} from './http';

export function createLocalDemoHandler(options:{repo:LocalDemoRepository;environment:string;enabled:boolean;now?:()=>number}):(request:Request)=>Promise<Response>{
  const now=options.now??Date.now;
  return async request=>{
    try{
      guardLocalRequest(request,options.environment,options.enabled,request.method!=='GET');
      let userId=getSelectedLocalUser(request);
      if(request.method==='GET') return localJSON(await loadLocalDemoState(options.repo,userId,now()));
      if(request.method!=='POST') throw new LocalHttpError(405,'INVALID');
      const body=await readLocalJSON(request);
      if(exactKeys(body,['action','userId'])&&body.action==='login'&&isLocalUserId(body.userId)){
        await options.repo.getUser(body.userId);
        return localJSON(await loadLocalDemoState(options.repo,body.userId,now()),200,{'Set-Cookie':selectionCookie(body.userId,request)});
      }
      if(exactKeys(body,['action'])&&body.action==='logout'){
        return localJSON(await loadLocalDemoState(options.repo,null,now()),200,{'Set-Cookie':selectionCookie(null,request)});
      }
      if(exactKeys(body,['action','materialId','kind'])&&body.action==='record'&&typeof body.materialId==='string'&&(body.kind==='view'||body.kind==='complete')){
        if(!userId)throw new LocalHttpError(401,'UNSELECTED');
        await options.repo.recordEvent(userId,body.materialId,body.kind,new Date(now()));
        return localJSON(await loadLocalDemoState(options.repo,userId,now()));
      }
      throw new LocalHttpError(400,'INVALID');
    }catch(error){
      if(error instanceof LocalHttpError)return localFailure(error.code,error.status);
      if(error instanceof LocalStoreError&&error.code==='INVALID_INPUT')return localFailure('INVALID',400);
      return localFailure('STORE_FAILED',500);
    }
  };
}
