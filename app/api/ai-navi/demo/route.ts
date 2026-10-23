import { createDemoHandler } from '../../../../server/ai-navi/demo-handler';

export const runtime='nodejs';
export const dynamic='force-dynamic';
export const POST=createDemoHandler({
  environment:process.env.NODE_ENV,
  enabled:process.env.MILIFE_AI_NAVI_DEMO==='local',
  config:{apiKey:process.env.OPENAI_API_KEY??'',model:process.env.OPENAI_MODEL??''},
});
