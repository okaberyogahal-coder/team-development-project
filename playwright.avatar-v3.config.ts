import {defineConfig} from '@playwright/test';
import smoke from './playwright.smoke.config';
export default defineConfig({...smoke,testMatch:['avatar-v3-editor.spec.ts','avatar-v3-flow.spec.ts','account-overview.spec.ts','avatar-brand.spec.ts','avatar-viewport.spec.ts','world-header.spec.ts','world-interaction.spec.ts','world-fullscreen.spec.ts','main-login-navigation.spec.ts'],grepInvert:undefined,timeout:180000,use:{...smoke.use,actionTimeout:12000}});
