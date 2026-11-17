import {createLocalFriendsHandler} from '../../../../server/local-demo/friends-handler';
import {getLocalDemoRepository} from '../../../../server/local-demo/repository';
import {getLocalSessionStore} from '../../../../server/local-demo/session';

export const runtime='nodejs';
export const dynamic='force-dynamic';

const handler=createLocalFriendsHandler({
  repo:getLocalDemoRepository(),
  sessions:getLocalSessionStore(),
  environment:process.env.NODE_ENV??'',
  enabled:process.env.MILIFE_AI_NAVI_DEMO==='local',
});

export const GET=handler;
export const POST=handler;
export const PATCH=handler;
export const DELETE=handler;
