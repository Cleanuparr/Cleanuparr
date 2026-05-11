import { test as base, Page } from '@playwright/test';
import { ApiClient, CleanuparrApi, ensureAdminAccount, waitForApp } from '../helpers/api';
import { resetDatabases, waitForDatabases } from '../helpers/db/reset';
import { MockServers } from '../helpers/mocks/wiremock-client';
import { TEST_CONFIG } from '../helpers/test-config';

interface WorkerFixtures {
  workerAdminTokens: { accessToken: string; refreshToken: string; expiresIn: number };
}

interface TestFixtures {
  autoReset: void;
  api: CleanuparrApi;
  anonymousApi: CleanuparrApi;
  mocks: MockServers;
  resetDb: () => Promise<void>;
  authenticatedPage: Page;
}

/**
 * Base Playwright test with Cleanuparr fixtures.
 *
 * Each test receives:
 *  - `api`            — a {@link CleanuparrApi} authenticated as the admin
 *  - `anonymousApi`   — a {@link CleanuparrApi} with no token (for 401 / anonymous flows)
 *  - `mocks`          — {@link MockServers} for stubbing external integrations
 *  - `resetDb`        — manual reset trigger (also runs automatically before each test)
 *  - `authenticatedPage` — Playwright page with the admin's tokens injected into localStorage
 *
 * Reset strategy: the test harness opens the app's SQLite database files
 * directly (mounted at ./.e2e-config) and deletes dynamic data + unlocks the
 * admin between tests. The production app stays unmodified.
 */
export const test = base.extend<TestFixtures, WorkerFixtures>({
  workerAdminTokens: [
    async ({}, use) => {
      const bootstrap = new CleanuparrApi();
      await waitForApp(bootstrap.client);
      await waitForDatabases();
      await ensureAdminAccount(bootstrap);
      const tokens = await bootstrap.auth.loginAndCaptureTokens(
        TEST_CONFIG.adminUsername,
        TEST_CONFIG.adminPassword,
      );
      await use(tokens);
    },
    { scope: 'worker' },
  ],

  // Auto-fixture: runs before every test, regardless of which other fixtures
  // the test consumes. Resets the on-disk SQLite databases (dynamic data
  // only — singleton configs are preserved) and clears any registered
  // WireMock stubs.
  autoReset: [
    async ({ mocks }, use) => {
      resetDatabases();
      await mocks.resetAll();
      await use();
    },
    { auto: true },
  ],

  api: async ({ workerAdminTokens }, use) => {
    const api = new CleanuparrApi({ token: workerAdminTokens.accessToken });
    await use(api);
  },

  anonymousApi: async ({}, use) => {
    await use(new CleanuparrApi());
  },

  mocks: async ({}, use) => {
    const servers = new MockServers();
    await use(servers);
  },

  resetDb: async ({}, use) => {
    await use(async () => {
      resetDatabases();
    });
  },

  authenticatedPage: async ({ workerAdminTokens, page }, use) => {
    await page.addInitScript((tokens) => {
      try {
        window.localStorage.setItem('cleanuparr.auth.tokens', JSON.stringify(tokens));
      } catch {
        // localStorage unavailable; ignore
      }
    }, workerAdminTokens);
    await page.goto('/');
    await use(page);
  },
});

export const expect = test.expect;
export { TEST_CONFIG, ApiClient, CleanuparrApi };
