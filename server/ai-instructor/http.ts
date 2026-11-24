import {LocalHttpError} from '../local-demo/http';
export async function readInstructorJSON(request:Request):Promise<unknown>{
 if(request.headers.get('content-type')?.split(';')[0].trim().toLowerCase()!=='application/json')throw new LocalHttpError(415,'INVALID');
 if(!request.body)throw new LocalHttpError(400,'INVALID');
 const reader=request.body.getReader();const chunks:Uint8Array[]=[];let length=0;
 try{
  while(true){const {done,value}=await reader.read();if(done)break;length+=value.length;
   if(length>16384){await reader.cancel();throw new LocalHttpError(413,'TOO_LARGE');}chunks.push(value);}
 }finally{reader.releaseLock();}
 const data=new Uint8Array(length);let offset=0;for(const chunk of chunks){data.set(chunk,offset);offset+=chunk.length;}
 try{return JSON.parse(new TextDecoder('utf-8',{fatal:true}).decode(data));}catch{throw new LocalHttpError(400,'INVALID');}
}
