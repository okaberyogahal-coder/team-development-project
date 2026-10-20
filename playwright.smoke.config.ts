import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests/browser',
  testMatch: ['auth.spec.ts', 'home-header.spec.ts', 'home-scroll-reveal.spec.ts', 'logo-image.spec.ts', 'unity-entry.spec.ts'],
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
  },
  reporter: 'list',
});
