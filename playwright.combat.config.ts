import {defineConfig} from '@playwright/test';
import unity from './playwright.unity.config';
export default defineConfig({...unity,testMatch:['world-combat.spec.ts'],timeout:240000});
