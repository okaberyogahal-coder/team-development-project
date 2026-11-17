import {headers} from 'next/headers';
import {notFound} from 'next/navigation';
import {isLocalDemoUrl} from '../../server/local-demo/http';
import {InstructorClient} from './InstructorClient';
import './instructor.css';
export const dynamic='force-dynamic';
export default async function InstructorPage(){
 const host=(await headers()).get('host');
 if(process.env.NODE_ENV!=='development'||process.env.MILIFE_AI_INSTRUCTOR_DEMO!=='local'||!host||!isLocalDemoUrl(`http://${host}`))notFound();
 return <InstructorClient/>;
}
