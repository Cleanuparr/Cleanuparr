import { defineConfig, Project } from '@playwright/test';

// Each spec folder is its own project, paired with a setup project.
// The setup restarts the app container and re-bootstraps the admin.
const FOLDERS = [
  'account',
  'arr',
  'auth',
  'blacklist-sync',
  'core',
  'download-cleaner',
  'download-client',
  'general',
  'live-arr',
  'live-arr-fast',
  'live-lazylibrarian',
  'malware-blocker',
  'notifications',
  'oidc',
  'queue-cleaner',
  'regression',
  'seeker',
  'signalr',
] as const;

function projectsFor(folder: string): Project[] {
  return [
    {
      name: `setup:${folder}`,
      testMatch: `tests/_setup/${folder}.setup.ts`,
      use: { browserName: 'chromium' },
    },
    {
      name: folder,
      testDir: `tests/${folder}`,
      use: { browserName: 'chromium' },
      dependencies: [`setup:${folder}`],
    },
  ];
}

export default defineConfig({
  testDir: './tests',
  globalSetup: './tests/global-setup.ts',
  timeout: 60_000,
  retries: 1,
  workers: 1, // Serial — projects share the single app container.
  use: {
    baseURL: 'http://localhost:5000',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: FOLDERS.flatMap(projectsFor),
  reporter: [['html', { open: 'never' }], ['list']],
});
