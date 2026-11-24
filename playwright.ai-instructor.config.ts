import {defineConfig} from '@playwright/test';
import {cpSync} from 'node:fs';
import {join} from 'node:path';
// Config is evaluated in workers too; use one dedicated path for this port-bound suite.
const fixture=process.env.MILIFE_LOCAL_DEMO_TEST_DIR??'/private/tmp/melife-ai-instructor-browser-fixture';
cpSync('tests/fixtures/local-demo',fixture,{recursive:true});
process.env.MILIFE_LOCAL_DEMO_TEST_DIR=fixture;
export default defineConfig({
 testDir:'./tests/browser',testMatch:'ai-instructor.spec.ts',workers:1,timeout:30000,
 use:{baseURL:'http://localhost:3004',viewport:{width:1440,height:1000},launchOptions:{executablePath:process.env.PLAYWRIGHT_EXECUTABLE_PATH??'/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'}},
 webServer:{command:'node scripts/dev-ai-instructor.mjs',url:'http://localhost:3004/api/local-demo',reuseExistingServer:false,timeout:120000,env:{MILIFE_LOCAL_DEMO_DIR:join(fixture),OPENAI_API_KEY:'',OPENAI_MODEL:''}},reporter:'list',
});
