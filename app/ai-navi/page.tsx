import { headers } from 'next/headers';
import { notFound } from 'next/navigation';
import { isLocalDemoUrl } from '../../server/ai-navi/demo-handler';
import {LocalDemoWorkspace} from './LocalDemoWorkspace';
import './demo.css';

export const dynamic='force-dynamic';
export default async function AiNaviDemoPage(){
  const host=(await headers()).get('host');
  if(process.env.NODE_ENV!=='development' || process.env.MILIFE_AI_NAVI_DEMO!=='local' || !host || !isLocalDemoUrl(`http://${host}`)) notFound();
  return <LocalDemoWorkspace/>;
}
