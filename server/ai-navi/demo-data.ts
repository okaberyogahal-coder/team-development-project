import { aggregatePopularity } from './popularity';
import { selectCandidates } from './candidates';
import type { Candidate, DiscoveryHistory, Material, PersonalInput, PopularitySummary } from './types';

export const demoMaterials: Material[] = [
  {id:'natural-light',title:'自然光と背景ぼけの基本',description:'窓辺の光と絞りを使った写真の撮り方を学ぶサンプル教材。',topics:['写真','カメラ'],published:true},
  {id:'flower-photo',title:'生花を美しく撮る、小さな写真教室',description:'切り花を被写体に、背景・光・構図を工夫するサンプル教材。',topics:['写真','カメラ','生花'],published:true},
  {id:'flower-arrange',title:'季節の花を楽しむ、はじめての花飾り',description:'花瓶と色の組み合わせを考えるサンプル教材。',topics:['生花','園芸'],published:true},
  {id:'cooking',title:'毎日に役立つ、やさしい料理',description:'手軽な献立の組み立てを学ぶサンプル教材。',topics:['料理'],published:true},
  {id:'digital',title:'スマートフォンで写真を整理する',description:'写真の整理や保存の基本を学ぶサンプル教材。',topics:['写真','デジタル'],published:true},
];

function history(materialId:string,kind:'view'|'complete'):DiscoveryHistory {
  const material=demoMaterials.find(item=>item.id===materialId)!;
  return {id:`${kind}:${materialId}`,materialId,kind,title:material.title,description:material.description,topics:material.topics};
}
export const demoProfiles = [
  {id:'photography',label:'写真 ＋ 花・自然の履歴',description:'写真の教材を受講完了し、花飾りの教材を閲覧したケース。興味・目標の申告はありません。',history:[history('natural-light','complete'),history('flower-arrange','view')]},
  {id:'food-photo',label:'写真 ＋ 料理の履歴',description:'写真の教材を受講完了し、料理の教材を閲覧したケース。興味・目標の申告はありません。',history:[history('natural-light','complete'),history('cooking','view')]},
  {id:'newcomer',label:'まだ履歴がないケース',description:'履歴がまだないため、みらいふ内で人気のテーマを新しい楽しみの入口にします。',history:[]},
];
export interface DemoContext {
  history: DiscoveryHistory[];
  candidates: (Material & {sources: Candidate['sources']})[];
  popularity: PopularitySummary;
}
export function createDemoContext(profileId:string,now:number):DemoContext {
  const profile=demoProfiles.find(item=>item.id===profileId);
  if(!profile) throw new Error('Unknown demo profile');
  const personal:PersonalInput={interests:[],goals:[],feedback:[],events:profile.history.map(item=>({userId:'demo-self',materialId:item.materialId,kind:item.kind,at:now-86400000}))};
  const events=Array.from({length:5},(_,index)=>({userId:`sample-${index}`,materialId:'cooking',kind:'view' as const,at:now-86400000}));
  events.push(...Array.from({length:4},(_,index)=>({userId:`sample-${index}`,materialId:'flower-photo',kind:'view' as const,at:now-86400000})));
  const popularity=aggregatePopularity(demoMaterials,events,now);
  const selected=selectCandidates(demoMaterials,personal,popularity);
  return {history:profile.history,candidates:selected.map(item=>({...demoMaterials.find(material=>material.id===item.materialId)!,sources:item.sources})),popularity};
}
