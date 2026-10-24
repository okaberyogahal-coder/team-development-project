import {createLocalDemoHandler} from '../../../server/local-demo/handler';
import {getLocalDemoRepository} from '../../../server/local-demo/repository';
export const runtime='nodejs';
export const dynamic='force-dynamic';
const handler=createLocalDemoHandler({repo:getLocalDemoRepository(),environment:process.env.NODE_ENV,enabled:process.env.MILIFE_AI_NAVI_DEMO==='local'});
export const GET=handler;
export const POST=handler;
