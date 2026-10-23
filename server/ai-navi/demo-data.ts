import { aggregatePopularity } from './popularity';
import { selectCandidates } from './candidates';
import type { Candidate, Material, PersonalInput, PopularitySummary } from './types';

export const demoMaterials: Material[] = [
  {id:'natural-light',title:'自然光と背景ぼけの基本',description:'窓辺の光と絞りを使った写真の撮り方を学ぶサンプル教材。',topics:['写真','カメラ'],published:true},
  {id:'flower-photo',title:'生花を美しく撮る、小さな写真教室',description:'切り花を被写体に、背景・光・構図を工夫するサンプル教材。',topics:['写真','カメラ','生花'],published:true},
  {id:'flower-arrange',title:'季節の花を楽しむ、はじめての花飾り',description:'花瓶と色の組み合わせを考えるサンプル教材。',topics:['生花','園芸'],published:true},
  {id:'cooking',title:'毎日に役立つ、やさしい料理',description:'手軽な献立の組み立てを学ぶサンプル教材。',topics:['料理'],published:true},
  {id:'digital',title:'スマートフォンで写真を整理する',description:'写真の整理や保存の基本を学ぶサンプル教材。',topics:['写真','デジタル'],published:true},
];
const empty: PersonalInput = {interests:[],goals:[],events:[],feedback:[]};
export const demoProfiles = [
  {id:'photography',label:'カメラが趣味',description:'写真に興味があり、自然光の教材を閲覧したサンプルです。',personal:{...empty,interests:['カメラ'],goals:['花の写真を撮りたい']}},
  {id:'newcomer',label:'初めて・履歴なし',description:'関心も履歴もまだないサンプルです。みらいふ内の人気から案内します。',personal:{...empty}},
];
export interface DemoContext {
  interests: string[];
  goals: string[];
  historyTopics: string[];
  candidates: (Material & {sources: Candidate['sources']})[];
  popularity: PopularitySummary;
}
export function createDemoContext(profileId: string, now: number): DemoContext {
  const profile = demoProfiles.find(item=>item.id===profileId);
  if (!profile) throw new Error('Unknown demo profile');
  const personal: PersonalInput = {...profile.personal,events:profileId==='photography'
    ? [{userId:'demo-self',materialId:'natural-light',kind:'view',at:now-86400000}] : []};
  const events = Array.from({length:5},(_,index)=>({userId:`sample-${index}`,materialId:'cooking',kind:'view' as const,at:now-86400000}));
  events.push(...Array.from({length:4},(_,index)=>({userId:`sample-${index}`,materialId:'flower-photo',kind:'view' as const,at:now-86400000})));
  const popularity=aggregatePopularity(demoMaterials,events,now);
  const selected=selectCandidates(demoMaterials,personal,popularity);
  return {
    interests:personal.interests,goals:personal.goals,
    historyTopics:profileId==='photography'?['写真','カメラ']:[],
    candidates:selected.map(item=>({...demoMaterials.find(material=>material.id===item.materialId)!,sources:item.sources})),
    popularity,
  };
}
