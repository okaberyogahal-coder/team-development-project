import { headers } from 'next/headers';
import { notFound } from 'next/navigation';
import { createDemoContext, demoMaterials, demoProfiles } from '../../server/ai-navi/demo-data';
import { isLocalDemoUrl } from '../../server/ai-navi/demo-handler';
import { DemoClient } from './DemoClient';
import './demo.css';

export const dynamic='force-dynamic';
export default function AiNaviDemoPage(){
  const host=headers().get('host');
  if(process.env.NODE_ENV!=='development' || process.env.MILIFE_AI_NAVI_DEMO!=='local' || !host || !isLocalDemoUrl(`http://${host}`)) notFound();
  const popularity=createDemoContext('newcomer',Date.now()).popularity;
  return <DemoClient profiles={demoProfiles.map(({id,label,description,history})=>({id,label,description,history}))}
    materials={demoMaterials} popularity={popularity}/>;
}
