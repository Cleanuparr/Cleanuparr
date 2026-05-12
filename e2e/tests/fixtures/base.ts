import { test as base, Page } from '@playwright/test';
import { ApiClient, CleanuparrApi } from '../helpers/api';
import { adminTokens } from '../helpers/test-lifecycle';
import { MockServers } from '../helpers/mocks/wiremock-client';
import { TEST_CONFIG } from '../helpers/test-config';

interface TestFixtures {
  autoReset: void;
  api: CleanuparrApi;
  anonymousApi: CleanuparrApi;
  mocks: MockServers;
  authenticatedPage: Page;
}

/**
 * Base Playwright test with Cleanuparr fixtures.
 *
 * Each test receives:
 *  - `api`            — a {@link CleanuparrApi} authenticated as the admin
 *  - `anonymousApi`   — a {@link CleanuparrApi} with no token (for 401 / anonymous flows)
 *  - `mocks`          — {@link MockServers} for stubbing external integrations
 *  - `authenticatedPage` — Playwright page with the admin's tokens injected into localStorage
 *
 * Isolation model: each spec folder gets a dedicated Playwright "setup"
 * project (`tests/_setup/<folder>.setup.ts`) that restarts the app
 * container, re-creates the admin account on the now-empty tmpfs `/config`,
 * and writes fresh bearer tokens to `playwright/.auth/admin.json`. Tests
 * within a folder cooperate (unique entity names + restore-after-modify);
 * the folder boundary is the hard reset.
 */
export const test = base.extend<TestFixtures>({
  // Auto-fixture: clear WireMock stubs before every test. The app's own
  // state is reset only between spec folders by the matching setup project.
  autoReset: [
    async ({ mocks }, use) => {
      await mocks.resetAll();
      await use();
    },
    { auto: true },
  ],

  api: async ({}, use) => {
    const tokens = adminTokens();
    await use(new CleanuparrApi({ token: tokens.accessToken }));
  },

  anonymousApi: async ({}, use) => {
    await use(new CleanuparrApi());
  },

  mocks: async ({}, use) => {
    const servers = new MockServers();
    await use(servers);
  },

  authenticatedPage: async ({ page }, use) => {
    const tokens = adminTokens();
    await page.addInitScript((t) => {
      try {
        window.localStorage.setItem('cleanuparr.auth.tokens', JSON.stringify(t));
      } catch {
        // localStorage unavailable; ignore
      }
    }, tokens);
    await page.goto('/');
    await use(page);
  },
});

export const expect = test.expect;
export { TEST_CONFIG, ApiClient, CleanuparrApi };
