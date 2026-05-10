import { test as base, Page } from '@playwright/test';
import { adminLogin, ApiClient, CleanuparrApi, ensureAdminAccount, waitForApp } from '../helpers/api';
import { MockServers } from '../helpers/mocks/wiremock-client';
import { TEST_CONFIG } from '../helpers/test-config';

interface WorkerFixtures {
  workerAdminTokens: { accessToken: string; refreshToken: string; expiresIn: number };
}

interface TestFixtures {
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
 *  - `resetDb`        — manual reset trigger (also called automatically before each test)
 *  - `authenticatedPage` — Playwright page with the admin's tokens injected into localStorage
 *
 * The DB is reset and mocks are cleared before each test. Admin account is created
 * once per worker.
 */
export const test = base.extend<TestFixtures, WorkerFixtures>({
  workerAdminTokens: [
    async ({}, use) => {
      const bootstrap = new CleanuparrApi();
      await waitForApp(bootstrap.client);
      await ensureAdminAccount(bootstrap);
      const tokens = await bootstrap.auth.loginAndCaptureTokens(
        TEST_CONFIG.adminUsername,
        TEST_CONFIG.adminPassword,
      );
      await use(tokens);
    },
    { scope: 'worker' },
  ],

  api: async ({ workerAdminTokens, mocks }, use) => {
    const api = new CleanuparrApi({ token: workerAdminTokens.accessToken });
    const reset = await api.testReset.reset();
    if (!reset.ok && reset.status !== 404) {
      throw new Error(`Test reset failed: ${reset.status}. Run backend with Cleanuparr:E2eMode=true.`);
    }
    if (reset.status === 404) {
      throw new Error(
        'Backend test reset endpoint returned 404. Set Cleanuparr:E2eMode=true (or CLEANUPARR_E2E_MODE=true) on the app container.',
      );
    }
    await mocks.resetAll();
    await use(api);
  },

  anonymousApi: async ({}, use) => {
    await use(new CleanuparrApi());
  },

  mocks: async ({}, use) => {
    const servers = new MockServers();
    await use(servers);
  },

  resetDb: async ({ api }, use) => {
    await use(async () => {
      await api.testReset.resetOrThrow();
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
