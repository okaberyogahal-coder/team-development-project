import {lstat,readFile,writeFile,rename,rm} from 'node:fs/promises';
import {join,resolve} from 'node:path';
import {randomUUID} from 'node:crypto';
import {LOCAL_USER_IDS,isLocalUserId,type LocalUserId,type LocalUser,type LocalEvent} from '../../shared/local-demo';
import type {Material} from '../ai-navi/types';

export interface LocalDemoRepository {
  listUsers():Promise<LocalUser[]>;
  listMaterials():Promise<Material[]>;
  getUser(id:LocalUserId):Promise<LocalUser>;
  recordEvent(id:LocalUserId,materialId:string,kind:LocalEvent['kind'],now:Date):Promise<LocalUser>;
}
export class LocalStoreError extends Error {
  constructor(public readonly code:'INVALID_DATA'|'INVALID_INPUT'|'SAVE_FAILED'){super(code);this.name='LocalStoreError';}
}
const scope=globalThis as typeof globalThis & {__melifeLocalQueues?:Map<string,Promise<unknown>>};
const queues=scope.__melifeLocalQueues??(scope.__melifeLocalQueues=new Map());
async function serialize<T>(key:string,action:()=>Promise<T>):Promise<T>{
  const current=(queues.get(key)??Promise.resolve()).catch(()=>{}).then(action);
  queues.set(key,current);
  try{return await current;}finally{if(queues.get(key)===current)queues.delete(key);}
}
function validText(value:unknown,max=500):value is string {return typeof value==='string'&&value.trim().length>0&&value.length<=max;}
const validId=(value:unknown):value is string=>typeof value==='string'&&/^[a-z][a-z0-9-]{0,63}$/.test(value);
function invalid():never{throw new LocalStoreError('INVALID_DATA');}
async function readJSON(file:string):Promise<any>{
  try{
    const stat=await lstat(file);
    if(!stat.isFile()||stat.size>1048576) return invalid();
    const bytes=await readFile(file);
    if(bytes.length>1048576) return invalid();
    return JSON.parse(bytes.toString('utf8'));
  }catch{ return invalid(); }
}
export class FileLocalDemoRepository implements LocalDemoRepository {
  private readonly root:string;
  constructor(root:string){this.root=resolve(root);}
  private userPath(id:LocalUserId):string{
    if(!isLocalUserId(id)) throw new LocalStoreError('INVALID_INPUT');
    return join(this.root,'users',`${id}.json`);
  }
  async listMaterials():Promise<Material[]>{
    const data=await readJSON(join(this.root,'materials.json'));
    if(data?.schemaVersion!==1||!Array.isArray(data.materials)||data.materials.length>1000) return invalid();
    const ids=new Set<string>();
    return data.materials.map((m:any)=>{
      if(!m||!validId(m.id)||ids.has(m.id)||!validText(m.title)||!validText(m.description,5000)||typeof m.published!=='boolean'||!Array.isArray(m.topics)||m.topics.length>30||!m.topics.every((x:unknown)=>validText(x))) return invalid();
      ids.add(m.id);
      return {id:m.id,title:m.title,description:m.description,topics:m.topics,published:m.published};
    });
  }
  async getUser(id:LocalUserId):Promise<LocalUser>{
    const path=this.userPath(id);
    const [data,materials]=await Promise.all([readJSON(path),this.listMaterials()]);
    const catalog=new Set(materials.map(m=>m.id));
    if(data?.schemaVersion!==1||data.id!==id||!validText(data.nickname,40)||!validText(data.description,1000)||!Array.isArray(data.events)||data.events.length>1000) return invalid();
    const ids=new Set<string>();
    const events:LocalEvent[]=data.events.map((e:any)=>{
      if(!e||(e.kind!=='view'&&e.kind!=='complete')||!catalog.has(e.materialId)||e.id!==`${e.kind}:${e.materialId}`||ids.has(e.id)||typeof e.at!=='string'||!Number.isFinite(Date.parse(e.at))||new Date(e.at).toISOString()!==e.at) return invalid();
      ids.add(e.id);
      return {id:e.id,materialId:e.materialId,kind:e.kind,at:e.at};
    });
    return {schemaVersion:1,id,nickname:data.nickname,description:data.description,events};
  }
  async listUsers():Promise<LocalUser[]>{return Promise.all(LOCAL_USER_IDS.map(id=>this.getUser(id)));}
  async recordEvent(id:LocalUserId,materialId:string,kind:LocalEvent['kind'],now:Date):Promise<LocalUser>{
    const path=this.userPath(id);
    if((kind!=='view'&&kind!=='complete')||!Number.isFinite(now.getTime())) throw new LocalStoreError('INVALID_INPUT');
    return serialize(path,async()=>{
      const materials=await this.listMaterials();
      if(!materials.some(m=>m.id===materialId&&m.published)) throw new LocalStoreError('INVALID_INPUT');
      const user=await this.getUser(id);
      const event:LocalEvent={id:`${kind}:${materialId}`,materialId,kind,at:now.toISOString()};
      const index=user.events.findIndex(e=>e.id===event.id);
      if(index>=0)user.events[index]=event;else user.events.push(event);
      if(user.events.length>1000) throw new LocalStoreError('INVALID_INPUT');
      const temporary=join(this.root,'users',`.${id}-${randomUUID()}.tmp`);
      try{
        await writeFile(temporary,JSON.stringify(user,null,2)+'\n',{flag:'wx',mode:0o600});
        await rename(temporary,path);
      }catch{throw new LocalStoreError('SAVE_FAILED');}
      finally{await rm(temporary,{force:true}).catch(()=>{});}
      return user;
    });
  }
}
const repositories=new Map<string,LocalDemoRepository>();
export function getLocalDemoRepository():LocalDemoRepository{
  const root=resolve(process.env.MILIFE_LOCAL_DEMO_DIR??join(process.cwd(),'data','local-demo'));
  if(!repositories.has(root))repositories.set(root,new FileLocalDemoRepository(root));
  return repositories.get(root)!;
}
