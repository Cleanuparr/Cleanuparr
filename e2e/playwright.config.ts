import { defineConfig, Project } from '@playwright/test';

// Each spec folder is its own Playwright project, paired with a "setup" project
// that restarts the app container and re-bootstraps the admin before the
// folder's tests run. Folder = isolation boundary.
const FOLDERS = [
  'account',
  'arr',
  'auth',
  'blacklist-sync',
  'core',
  'download-cleaner',
  'download-client',
  'general',
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
