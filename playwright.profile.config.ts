import config from './playwright.smoke.config';
import {defineConfig} from '@playwright/test';
export default defineConfig({...config,testMatch:'profile-edit-auth.spec.ts'});
