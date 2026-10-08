import {defineConfig} from '@playwright/test';
const baseURL=process.env.PLAZA_TEST_URL || 'http://localhost:3002';
export default defineConfig({testDir:'./tests/browser',workers:1,timeout:30000,use:{baseURL,viewport:{width:1440,height:1000},launchOptions:{executablePath:'/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'}},webServer:{command:'npm run dev:plaza',url:'http://localhost:3005/health',reuseExistingServer:false,env:{PLAZA_PORT:'3005',PLAZA_PAGE_PORT:new URL(baseURL).port,PLAZA_ALLOWED_ORIGINS:'',LIVEKIT_URL:'',LIVEKIT_API_KEY:'',LIVEKIT_API_SECRET:''}},reporter:'list'});
