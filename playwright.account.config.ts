import {defineConfig} from '@playwright/test';
import smoke from './playwright.smoke.config';
export default defineConfig({...smoke,testMatch:'account-overview.spec.ts'});
