import 'server-only';
import {createInstructorHandler} from '../../../server/ai-instructor/handler';
import {InstructorStore} from '../../../server/ai-instructor/store';
export const runtime='nodejs';
export const dynamic='force-dynamic';
const state=globalThis as typeof globalThis&{__melifeInstructor?:{store:InstructorStore;timer:ReturnType<typeof setInterval>}};
if(!state.__melifeInstructor){
 const store=new InstructorStore();
 const timer=setInterval(()=>store.sweep(Date.now()),60000);timer.unref();
 state.__melifeInstructor={store,timer};
}
const handler=createInstructorHandler({environment:process.env.NODE_ENV,enabled:process.env.MILIFE_AI_INSTRUCTOR_DEMO==='local',store:state.__melifeInstructor.store,config:{apiKey:process.env.OPENAI_API_KEY??'',model:process.env.OPENAI_MODEL??''}});
export const GET=handler;
export const POST=handler;
