import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  globalSetup: './tests/global-setup.ts',
  timeout: 60_000,
  retries: 1,
  workers: 1,
  use: {
    baseURL: 'http://localhost:5000',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: [
    {
      name: 'api',
      testMatch: ['**/*.api.spec.ts'],
      use: { browserName: 'chromium' },
    },
    {
      name: 'ui',
      testMatch: ['**/*.ui.spec.ts'],
      use: { browserName: 'chromium' },
    },
    {
      name: 'regression',
      testDir: './tests/regression',
      testMatch: ['**/*.spec.ts'],
      use: { browserName: 'chromium' },
    },
    {
      name: 'download-clients',
      testMatch: /(?:^|[\\/])(?:orphaned-files-cleanup|orphaned-files-behaviors|orphaned-files-unreachable-client|malware-blocker|dead-torrent-cleanup)\.spec\.ts$/,
      use: { browserName: 'chromium' },
    },
  ],
  reporter: [['html', { open: 'never' }], ['list']],
});
