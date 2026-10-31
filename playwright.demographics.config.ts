import {defineConfig} from '@playwright/test';

export default defineConfig({
 testDir:'./tests/browser',testMatch:'profile-demographics.spec.ts',workers:1,
 use:{baseURL:'http://127.0.0.1:3014',viewport:{width:1280,height:900},launchOptions:{executablePath:'/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'}},
 webServer:{command:'node node_modules/next/dist/bin/next dev --webpack --hostname 127.0.0.1 --port 3014',url:'http://127.0.0.1:3014',reuseExistingServer:false,timeout:120000},
 reporter:'list',
});
