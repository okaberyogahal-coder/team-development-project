import type {LocalDemoState} from './local-demo';
export class LocalClientError extends Error {constructor(message:string,public readonly status:number){super(message);}}
export async function readLocalState(signal?:AbortSignal):Promise<LocalDemoState>{
  const response=await fetch('/api/local-demo',{cache:'no-store',signal});
  const result=await response.json();
  if(!response.ok)throw new LocalClientError(result.error?.message??'開発用データを取得できませんでした。',response.status);
  return result;
}
export async function changeLocalState(body:unknown):Promise<LocalDemoState>{
  const response=await fetch('/api/local-demo',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)});
  const result=await response.json();
  if(!response.ok)throw new LocalClientError(result.error?.message??'開発用データを保存できませんでした。',response.status);
  return result;
}
