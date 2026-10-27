import { defineConfig } from '@playwright/test';

// Run against the local website after generating its Unity Web build.
export default defineConfig({
  testDir: './tests/browser',
  testMatch: 'unity-entry.spec.ts',
  workers: 1,
  timeout: 30000,
  use: {
    baseURL: process.env.PLAZA_TEST_URL || 'http://127.0.0.1:3002',
    viewport: { width: 1440, height: 1000 },
    launchOptions: process.env.PLAYWRIGHT_EXECUTABLE_PATH
      ? { executablePath: process.env.PLAYWRIGHT_EXECUTABLE_PATH }
      : undefined,
  },
  reporter: 'list',
});
