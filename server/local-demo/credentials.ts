import {scrypt,timingSafeEqual} from 'node:crypto';
import {open} from 'node:fs/promises';
import {constants} from 'node:fs';
import {join} from 'node:path';
import {isLocalUserId,type LocalUserId,LOCAL_USER_IDS} from '../../shared/local-demo';
import {LocalStoreError} from './repository';

export type CredentialVerifier=(email:string,password:string)=>Promise<LocalUserId|null>;
type Account={userId:LocalUserId;email:string;salt:string;hash:string};
const derive=(password:string,salt:string)=>new Promise<Buffer>((resolve,reject)=>{
 scrypt(password,Buffer.from(salt,'hex'),64,{N:16384,r:8,p:1},(error,key)=>error?reject(error):resolve(key));
});
export function createCredentialVerifier(root:string):CredentialVerifier{
 return async(email,password)=>{
  if(email.length>254||!email.trim()||password.length>128||!password)return null;
  const file=await open(join(root,'credentials.json'),constants.O_RDONLY|constants.O_NOFOLLOW);
  let value:unknown;
  try{const stat=await file.stat();if(!stat.isFile()||stat.size>1048576)throw new Error('Invalid credentials');value=JSON.parse(await file.readFile('utf8'));}finally{await file.close();}
  const data=value as {schemaVersion?:unknown;accounts?:unknown};
  if(data?.schemaVersion!==1||!Array.isArray(data.accounts)||data.accounts.length!==LOCAL_USER_IDS.length)throw new LocalStoreError('INVALID_DATA');
  const accounts:Account[]=[];const ids=new Set<string>();const emails=new Set<string>();
  for(const item of data.accounts){
   if(!item||!isLocalUserId(item.userId)||typeof item.email!=='string'||item.email!==item.email.trim().toLowerCase()||item.email.length>254||!/^\S+@\S+\.\S+$/.test(item.email)||typeof item.salt!=='string'||!/^[a-f0-9]{32}$/.test(item.salt)||typeof item.hash!=='string'||!/^[a-f0-9]{128}$/.test(item.hash)||ids.has(item.userId)||emails.has(item.email))throw new LocalStoreError('INVALID_DATA');
   ids.add(item.userId);emails.add(item.email);accounts.push(item);
  }
  const account=accounts.find(item=>item.email===email.trim().toLowerCase());
  // Unknown emails take the same scrypt path; only the final match differs.
  const comparison=account??accounts[0];
  const key=await derive(password,comparison.salt);
  const equal=timingSafeEqual(key,Buffer.from(comparison.hash,'hex'));
  return account&&equal?account.userId:null;
 };
}

/** One local process: reserve a slot before async hashing to bound concurrent attempts too. */
export class LoginAttemptLimiter{
 private attempts:{at:number}[]=[];
 begin(now:number):{at:number}|null{
  this.attempts=this.attempts.filter(item=>item.at>now-600000);
  if(this.attempts.length>=20)return null;
  const attempt={at:now};this.attempts.push(attempt);return attempt;
 }
 succeed(attempt:{at:number}):void{this.attempts=this.attempts.filter(item=>item!==attempt);}
}
const processAttempts=globalThis as typeof globalThis&{__melifeLoginAttempts?:LoginAttemptLimiter};
export function getLoginAttempts():LoginAttemptLimiter{return processAttempts.__melifeLoginAttempts??=new LoginAttemptLimiter();}
