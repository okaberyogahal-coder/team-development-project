import {resolve} from 'node:path';
import {createCredentialVerifier,getLoginAttempts,type CredentialVerifier,type LoginAttemptLimiter} from './credentials';
import type {LocalDemoRepository} from './repository';
import {LocalStoreError} from './repository';
import {loadLocalDemoState} from './state';
import {getLocalSessionStore,getSessionToken,sessionCookie,type LocalSessionStore} from './session';
import {LocalHttpError,exactKeys,guardLocalRequest,localFailure,localJSON,readLocalJSON} from './http';

export function createLocalDemoHandler(options:{repo:LocalDemoRepository;environment:string;enabled:boolean;now?:()=>number;storeFingerprint?:string;verify?:CredentialVerifier;sessions?:LocalSessionStore;attempts?:LoginAttemptLimiter}):(request:Request)=>Promise<Response>{
  const now=options.now??Date.now;
  const sessions=options.sessions??getLocalSessionStore();
  const attempts=options.attempts??getLoginAttempts();
  const verify=options.verify??createCredentialVerifier(resolve(process.env.MILIFE_LOCAL_DEMO_DIR??'data/local-demo'));
  return async request=>{
    try{
      guardLocalRequest(request,options.environment,options.enabled,request.method!=='GET');
      const token=getSessionToken(request);
      const userId=sessions.resolve(token,now());
      if(request.method==='GET') return localJSON(await loadLocalDemoState(options.repo,userId,now()),200,{'X-Melife-Local-Store':options.storeFingerprint??'shared'});
      if(request.method!=='POST') throw new LocalHttpError(405,'INVALID');
      const body=await readLocalJSON(request);
      if(exactKeys(body,['action','email','password'])&&body.action==='login'&&typeof body.email==='string'&&typeof body.password==='string'){
        if(!body.email.trim()||body.email.length>254||!body.password||body.password.length>128)throw new LocalHttpError(400,'INVALID');
        const attempt=attempts.begin(now());if(!attempt)throw new LocalHttpError(429,'LOGIN_RATE_LIMIT');
        const id=await verify(body.email,body.password);
        if(!id)throw new LocalHttpError(401,'INVALID_CREDENTIALS');
        const state=await loadLocalDemoState(options.repo,id,now());
        attempts.succeed(attempt);sessions.revoke(token);
        return localJSON(state,200,{'Set-Cookie':sessionCookie(sessions.issue(id,now()),request)});
      }
      if(exactKeys(body,['action'])&&body.action==='logout'){
        const state=await loadLocalDemoState(options.repo,null,now());
        sessions.revoke(token);
        return localJSON(state,200,{'Set-Cookie':sessionCookie(null,request)});
      }
      if(exactKeys(body,['action','materialId','kind'])&&body.action==='record'&&typeof body.materialId==='string'&&(body.kind==='view'||body.kind==='complete')){
        if(!userId)throw new LocalHttpError(401,'UNSELECTED');
        const state=await loadLocalDemoState(options.repo,userId,now());
        if(sessions.resolve(token,now())!==userId)throw new LocalHttpError(401,'UNSELECTED');
        if(!request.headers.get('if-match'))throw new LocalHttpError(400,'INVALID');
        if(request.headers.get('if-match')!==state.revision)throw new LocalHttpError(409,'STALE');
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
