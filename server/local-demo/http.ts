export class LocalHttpError extends Error {
  constructor(public readonly status:number,public readonly code:string){super(code);}
}
export function isLocalDemoUrl(value:string):boolean{
  try{const url=new URL(value);return ['http:','https:'].includes(url.protocol)&&['localhost','127.0.0.1','[::1]'].includes(url.hostname);}catch{return false;}
}
export function guardLocalRequest(request:Request,environment:string,enabled:boolean,write:boolean):void{
  if(environment!=='development'||!enabled) throw new LocalHttpError(404,'NOT_FOUND');
  const url=new URL(request.url);
  if(!isLocalDemoUrl(request.url)||request.headers.get('host')!==url.host||(write&&request.headers.get('origin')!==url.origin)) throw new LocalHttpError(403,'FORBIDDEN');
}
export function localJSON(value:unknown,status=200,headers:Record<string,string>={}):Response{
  return Response.json(value,{status,headers:{'Cache-Control':'no-store',...headers}});
}
export function localFailure(code:string,status:number,message?:string):Response{
  return localJSON({error:{code,message:message??(code==='INVALID_CREDENTIALS'?'メールアドレスまたはパスワードが違います。':code==='LOGIN_RATE_LIMIT'?'ログインの試行回数が上限に達しました。10分ほど待ってからお試しください。':code==='STALE'?'ユーザーまたは履歴が変更されました。表示を更新したので、もう一度操作してください。':code==='UNSELECTED'?'ログインしてください。':code==='STORE_FAILED'?'保存ファイルを読み書きできませんでした。JSONと保存先を確認してください。':'操作内容を確認してください。')}},status);
}
export async function readLocalJSON(request:Request):Promise<unknown>{
  if(request.headers.get('content-type')?.split(';')[0].trim().toLowerCase()!=='application/json') throw new LocalHttpError(415,'INVALID');
  if(!request.body) throw new LocalHttpError(400,'INVALID');
  const reader=request.body.getReader();const chunks:Uint8Array[]=[];let total=0;
  try{
    while(true){const {done,value}=await reader.read();if(done)break;total+=value.byteLength;if(total>1024){await reader.cancel();throw new LocalHttpError(413,'INVALID');}chunks.push(value);}
  }finally{reader.releaseLock();}
  const bytes=new Uint8Array(total);let at=0;for(const chunk of chunks){bytes.set(chunk,at);at+=chunk.length;}
  try{return JSON.parse(new TextDecoder().decode(bytes));}catch{throw new LocalHttpError(400,'INVALID');}
}
export function exactKeys(value:unknown,keys:string[]):value is Record<string,unknown>{
  return !!value&&typeof value==='object'&&!Array.isArray(value)&&Object.keys(value).length===keys.length&&keys.every(key=>Object.hasOwn(value,key));
}
