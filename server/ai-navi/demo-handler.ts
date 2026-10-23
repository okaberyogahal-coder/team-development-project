import { createDemoContext, demoProfiles } from './demo-data';
import { AiNaviError, generateSuggestions } from './openai';

export function isLocalDemoUrl(value: string): boolean {
  try {
    const url=new URL(value);
    return ['http:','https:'].includes(url.protocol) && ['localhost','127.0.0.1','[::1]'].includes(url.hostname);
  } catch { return false; }
}
const messages: Record<string,string>={
  UNCONFIGURED:'サーバーの OPENAI_API_KEY と OPENAI_MODEL を設定し、再起動してください。',
  TIMEOUT:'提案に時間がかかっています。少し待ってからもう一度お試しください。',
  UPSTREAM:'AIに接続できませんでした。APIの設定や利用状況を確認してください。',
  INVALID_OUTPUT:'提案の形式を確認できませんでした。もう一度お試しください。',
  REFUSED:'今回はAIから提案を取得できませんでした。別のサンプルをお試しください。',
  RATE_LIMIT:'実行回数の上限です。60秒ほど待ってからお試しください。',
  BUSY:'前の提案を処理しています。完了までお待ちください。',
  INVALID:'サンプルの選択内容を確認してください。',
};
function json(value: unknown,status=200): Response {
  return Response.json(value,{status,headers:{'Cache-Control':'no-store'}});
}
function failure(code:string,status:number): Response {
  return json({error:{code,message:messages[code]??'この操作は利用できません。'}},status);
}
async function readBody(request:Request):Promise<unknown> {
  if (!request.body) throw new Error('INVALID');
  const reader=request.body.getReader();
  const chunks:Uint8Array[]=[];let length=0;
  try {
    while(true){
      const {done,value}=await reader.read();if(done) break;
      length+=value.byteLength;
      if(length>1024){await reader.cancel();throw new Error('TOO_LARGE');}
      chunks.push(value);
    }
  } finally { reader.releaseLock(); }
  const buffer=new Uint8Array(length);let offset=0;
  for(const chunk of chunks){buffer.set(chunk,offset);offset+=chunk.length;}
  try {return JSON.parse(new TextDecoder().decode(buffer));}catch{throw new Error('INVALID');}
}
export function createDemoHandler(options:{
  environment:string;enabled?:boolean;config:{apiKey:string;model:string};generate?:typeof generateSuggestions;now?:()=>number;
}): (request:Request)=>Promise<Response> {
  const generate=options.generate??generateSuggestions;
  const clock=options.now??Date.now;
  let requests:number[]=[];let busy=false;
  return async(request)=>{
    if(options.environment!=='development' || !options.enabled) return failure('NOT_FOUND',404);
    const url=new URL(request.url);
    if(!isLocalDemoUrl(request.url) || request.headers.get('host')!==url.host || request.headers.get('origin')!==url.origin) return failure('FORBIDDEN',403);
    if(request.method!=='POST') return failure('INVALID',405);
    if(request.headers.get('content-type')?.split(';')[0].trim().toLowerCase()!=='application/json') return failure('INVALID',415);
    let input:unknown;
    try{input=await readBody(request);}catch(error){return failure('INVALID',error instanceof Error&&error.message==='TOO_LARGE'?413:400);}
    if(!input || typeof input!=='object' || Array.isArray(input) || Object.keys(input).length!==1 || !('profileId' in input) || !demoProfiles.some(item=>item.id===input.profileId)) return failure('INVALID',400);
    if(!options.config.apiKey.trim() || !options.config.model.trim()) return failure('UNCONFIGURED',503);
    if(busy) return failure('BUSY',429);
    const now=clock();requests=requests.filter(at=>at>now-60000);
    if(requests.length>=3) return failure('RATE_LIMIT',429);
    busy=true;requests.push(now);
    try{
      const context=createDemoContext(String(input.profileId),now);
      const suggestions=await generate(context,options.config);
      return json({suggestions,popularity:context.popularity});
    }catch(error){
      const code=error instanceof AiNaviError?error.code:'UPSTREAM';
      return failure(code,code==='TIMEOUT'?504:code==='UNCONFIGURED'?503:502);
    }finally{busy=false;}
  };
}
