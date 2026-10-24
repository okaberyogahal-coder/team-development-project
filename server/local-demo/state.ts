import {createHash} from 'node:crypto';
import type {LocalDemoState,LocalUserId} from '../../shared/local-demo';
import type {LocalDemoRepository} from './repository';
import {aggregatePopularity} from '../ai-navi/popularity';
import {selectCandidates} from '../ai-navi/candidates';
import type {DemoContext} from '../ai-navi/demo-data';
import type {LearningEvent} from '../ai-navi/types';

export async function loadLocalDemoState(repo:LocalDemoRepository,userId:LocalUserId|null,now:number):Promise<LocalDemoState>{
  const [users,materials]=await Promise.all([repo.listUsers(),repo.listMaterials()]);
  const currentUser=users.find(user=>user.id===userId)??null;
  const events:LearningEvent[]=users.flatMap(user=>user.events.map(event=>({userId:user.id,materialId:event.materialId,kind:event.kind,at:Date.parse(event.at)})));
  const revision=createHash('sha256').update(JSON.stringify({userId,users,materials})).digest('hex');
  return {users:users.map(({id,nickname,description})=>({id,nickname,description})),currentUser,materials:materials.filter(m=>m.published),popularity:aggregatePopularity(materials,events,now),revision};
}
export function buildStoredContext(state:LocalDemoState):DemoContext{
  if(!state.currentUser) throw new Error('No selected local user');
  const user=state.currentUser;
  const events:LearningEvent[]=user.events.map(event=>({userId:user.id,materialId:event.materialId,kind:event.kind,at:Date.parse(event.at)}));
  const selected=selectCandidates(state.materials,{interests:[],goals:[],feedback:[],events},state.popularity);
  const history=user.events.flatMap(event=>{
    const material=state.materials.find(m=>m.id===event.materialId);
    return material?[{id:event.id,materialId:event.materialId,kind:event.kind,title:material.title,description:material.description,topics:material.topics}]:[];
  });
  return {history,candidates:selected.map(item=>({...state.materials.find(m=>m.id===item.materialId)!,sources:item.sources})),popularity:state.popularity};
}
