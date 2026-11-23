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
export interface LocalDemoFriendsRepository extends LocalDemoRepository {
  readFriendPage(id:LocalUserId,nickname?:string):Promise<LocalFriendPage>;
  requestFriend(id:LocalUserId,targetId:LocalUserId,now:Date):Promise<void>;
  respondToFriendRequest(id:LocalUserId,requesterId:LocalUserId,response:'accept'|'decline',now:Date):Promise<void>;
  removeFriend(id:LocalUserId,targetId:LocalUserId):Promise<void>;
}
export interface LocalFriendProfile {id:LocalUserId;nickname:string}
export interface LocalFriendPage {
  friends:LocalFriendProfile[];
  incoming:Array<LocalFriendProfile&{createdAt:string}>;
  outgoing:Array<LocalFriendProfile&{createdAt:string}>;
  matches:Array<LocalFriendProfile&{connectionStatus:'none'|'friend'|'incoming'|'outgoing'}>;
}
type LocalFriendConnection={userAId:LocalUserId;userBId:LocalUserId;requestedById:LocalUserId;status:'PENDING'|'ACCEPTED'|'DECLINED';createdAt:string;updatedAt:string};
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
function orderedPair(id:LocalUserId,targetId:LocalUserId){
  if(id===targetId)throw new LocalStoreError('INVALID_INPUT');
  return id<targetId?{userAId:id,userBId:targetId}:{userAId:targetId,userBId:id};
}
function connectionKey(connection:Pick<LocalFriendConnection,'userAId'|'userBId'>){return `${connection.userAId}:${connection.userBId}`;}
async function readJSON(file:string):Promise<any>{
  try{
    const stat=await lstat(file);
    if(!stat.isFile()||stat.size>1048576) return invalid();
    const bytes=await readFile(file);
    if(bytes.length>1048576) return invalid();
    return JSON.parse(bytes.toString('utf8'));
  }catch{ return invalid(); }
}
export class FileLocalDemoRepository implements LocalDemoFriendsRepository {
  private readonly root:string;
  constructor(root:string){this.root=resolve(root);}
  private friendPath(){return join(this.root,'friends.json');}
  private async readConnections():Promise<LocalFriendConnection[]>{
    const data=await readJSON(this.friendPath());
    if(data?.schemaVersion!==1||!Array.isArray(data.connections)||data.connections.length>10000)return invalid();
    const ids=new Set<string>();
    return data.connections.map((item:any)=>{
      if(!item||!isLocalUserId(item.userAId)||!isLocalUserId(item.userBId)||!isLocalUserId(item.requestedById)||
        item.userAId>=item.userBId||![item.userAId,item.userBId].includes(item.requestedById)||
        !['PENDING','ACCEPTED','DECLINED'].includes(item.status)||
        typeof item.createdAt!=='string'||!Number.isFinite(Date.parse(item.createdAt))||new Date(item.createdAt).toISOString()!==item.createdAt||
        typeof item.updatedAt!=='string'||!Number.isFinite(Date.parse(item.updatedAt))||new Date(item.updatedAt).toISOString()!==item.updatedAt||
        ids.has(connectionKey(item)))return invalid();
      ids.add(connectionKey(item));
      return {userAId:item.userAId,userBId:item.userBId,requestedById:item.requestedById,status:item.status,createdAt:item.createdAt,updatedAt:item.updatedAt};
    });
  }
  private async saveConnections(connections:LocalFriendConnection[]):Promise<void>{
    const temporary=join(this.root,`.friends-${randomUUID()}.tmp`);
    try{
      await writeFile(temporary,JSON.stringify({schemaVersion:1,connections},null,2)+'\n',{flag:'wx',mode:0o600});
      await rename(temporary,this.friendPath());
    }catch{throw new LocalStoreError('SAVE_FAILED');}
    finally{await rm(temporary,{force:true}).catch(()=>{});}
  }
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
  async readFriendPage(id:LocalUserId,nickname?:string):Promise<LocalFriendPage>{
    const [connections,users]=await Promise.all([this.readConnections(),this.listUsers()]);
    const userById=new Map(users.map(user=>[user.id,user]));
    const toProfile=(targetId:LocalUserId):LocalFriendProfile=>{
      const user=userById.get(targetId);
      if(!user)throw new LocalStoreError('INVALID_DATA');
      return {id:targetId,nickname:user.nickname};
    };
    const mine=connections.filter(connection=>connection.userAId===id||connection.userBId===id);
    const related=(connection:LocalFriendConnection)=>toProfile(connection.userAId===id?connection.userBId:connection.userAId);
    const active=mine.filter(connection=>connection.status==='PENDING'||connection.status==='ACCEPTED');
    const search=nickname?.toLocaleLowerCase('ja-JP');
    const matches=search?users.filter(user=>user.id!==id&&user.nickname.toLocaleLowerCase('ja-JP')===search).map(user=>{
      const connection=mine.find(item=>item.userAId===user.id||item.userBId===user.id);
      const connectionStatus=connection?.status==='ACCEPTED'?'friend':
        connection?.status==='PENDING'?connection.requestedById===id?'outgoing':'incoming':'none';
      return {id:user.id,nickname:user.nickname,connectionStatus} as LocalFriendPage['matches'][number];
    }):[];
    return {
      friends:active.filter(connection=>connection.status==='ACCEPTED').map(related),
      incoming:active.filter(connection=>connection.status==='PENDING'&&connection.requestedById!==id).map(connection=>({...related(connection),createdAt:connection.createdAt})),
      outgoing:active.filter(connection=>connection.status==='PENDING'&&connection.requestedById===id).map(connection=>({...related(connection),createdAt:connection.createdAt})),
      matches,
    };
  }
  async requestFriend(id:LocalUserId,targetId:LocalUserId,now:Date):Promise<void>{
    const pair=orderedPair(id,targetId);
    if(!Number.isFinite(now.getTime()))throw new LocalStoreError('INVALID_INPUT');
    await serialize(this.friendPath(),async()=>{
      const connections=await this.readConnections();
      const index=connections.findIndex(connection=>connection.userAId===pair.userAId&&connection.userBId===pair.userBId);
      if(index>=0&&connections[index].status!=='DECLINED')throw new LocalStoreError('INVALID_INPUT');
      const timestamp=now.toISOString();
      const connection:LocalFriendConnection={...pair,requestedById:id,status:'PENDING',createdAt:timestamp,updatedAt:timestamp};
      if(index>=0)connections[index]=connection;else connections.push(connection);
      await this.saveConnections(connections);
    });
  }
  async respondToFriendRequest(id:LocalUserId,requesterId:LocalUserId,response:'accept'|'decline',now:Date):Promise<void>{
    const pair=orderedPair(id,requesterId);
    if(!Number.isFinite(now.getTime()))throw new LocalStoreError('INVALID_INPUT');
    await serialize(this.friendPath(),async()=>{
      const connections=await this.readConnections();
      const connection=connections.find(item=>item.userAId===pair.userAId&&item.userBId===pair.userBId&&item.status==='PENDING'&&item.requestedById===requesterId);
      if(!connection)throw new LocalStoreError('INVALID_INPUT');
      connection.status=response==='accept'?'ACCEPTED':'DECLINED';
      connection.updatedAt=now.toISOString();
      await this.saveConnections(connections);
    });
  }
  async removeFriend(id:LocalUserId,targetId:LocalUserId):Promise<void>{
    const pair=orderedPair(id,targetId);
    await serialize(this.friendPath(),async()=>{
      const connections=await this.readConnections();
      const next=connections.filter(connection=>!(connection.userAId===pair.userAId&&connection.userBId===pair.userBId&&
        (connection.status==='ACCEPTED'||(connection.status==='PENDING'&&connection.requestedById===id))));
      if(next.length===connections.length)throw new LocalStoreError('INVALID_INPUT');
      await this.saveConnections(next);
    });
  }
}
const repositories=new Map<string,LocalDemoFriendsRepository>();
export function getLocalDemoRepository():LocalDemoFriendsRepository{
  const root=resolve(process.env.MILIFE_LOCAL_DEMO_DIR??join(process.cwd(),'data','local-demo'));
  if(!repositories.has(root))repositories.set(root,new FileLocalDemoRepository(root));
  return repositories.get(root)!;
}
