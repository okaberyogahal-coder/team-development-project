import { defineConfig } from '@playwright/test';
import smoke from './playwright.smoke.config';

// Own an isolated server/database, and exercise the generated WebGL player.
export default defineConfig({
  ...smoke,
  testMatch: 'unity-auth.spec.ts',
  grepInvert: undefined,
  timeout: 180000,
  use: { ...smoke.use, actionTimeout: 10000 },
});
