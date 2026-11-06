import {defineConfig} from '@playwright/test';
import smoke from './playwright.smoke.config';
export default defineConfig({...smoke,testMatch:['avatar-v3-editor.spec.ts','avatar-v3-flow.spec.ts','account-overview.spec.ts'],grepInvert:undefined,timeout:180000,use:{...smoke.use,actionTimeout:12000}});
