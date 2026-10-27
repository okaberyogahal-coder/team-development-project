import { defineConfig } from '@playwright/test';
import { randomBytes } from 'node:crypto';
import { authTestDatabaseUrl } from './tests/support/auth-database';

const databaseUrl = authTestDatabaseUrl();

export default defineConfig({
  testDir: './tests/browser',
  testMatch: ['auth.spec.ts', 'auth-flow.spec.ts', 'home-header.spec.ts', 'home-scroll-reveal.spec.ts', 'logo-image.spec.ts', 'unity-entry.spec.ts'],
  // Unity's generated Web build is not required for the Next.js regression suite.
  grepInvert: /Unityワールド本体がブラウザー内で起動する/,
  workers: 1,
  timeout: 30000,
  use: {
    baseURL: 'http://127.0.0.1:3012',
    viewport: { width: 1440, height: 1000 },
    launchOptions: process.env.PLAYWRIGHT_EXECUTABLE_PATH
      ? { executablePath: process.env.PLAYWRIGHT_EXECUTABLE_PATH }
      : undefined,
  },
  webServer: {
    command: 'node node_modules/next/dist/bin/next start --hostname 127.0.0.1 --port 3012',
    url: 'http://127.0.0.1:3012',
    reuseExistingServer: false,
    timeout: 30000,
    env: {
      DATABASE_URL: databaseUrl, DIRECT_URL: databaseUrl,
      BETTER_AUTH_URL: 'http://127.0.0.1:3012',
      BETTER_AUTH_SECRET: randomBytes(48).toString('base64url'),
    },
  },
  reporter: 'list',
});
