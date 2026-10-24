import {createHash} from 'node:crypto';
import {resolve} from 'node:path';
import {createLocalDemoHandler} from '../../../server/local-demo/handler';
import {getLocalDemoRepository} from '../../../server/local-demo/repository';
export const runtime='nodejs';
export const dynamic='force-dynamic';
const handler=createLocalDemoHandler({repo:getLocalDemoRepository(),environment:process.env.NODE_ENV,enabled:process.env.MILIFE_AI_NAVI_DEMO==='local',storeFingerprint:process.env.MILIFE_LOCAL_DEMO_DIR?createHash('sha256').update(resolve(process.env.MILIFE_LOCAL_DEMO_DIR)).digest('hex'):undefined});
export const GET=handler;
export const POST=handler;
