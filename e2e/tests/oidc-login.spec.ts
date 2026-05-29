import { test, expect } from '@playwright/test';
import { TEST_CONFIG } from './helpers/test-config';
import {
  clearOidcLink,
  configureOidc,
  getOidcConfig,
  loginAndGetToken,
  OidcConfigSnapshot,
  setOidcConfig,
} from './helpers/app-api';
import { linkOidcViaBrowser } from './helpers/oidc';

test.describe('OIDC Login', () => {
  let token: string;
  let snapshot: OidcConfigSnapshot;

  test.beforeAll(async ({ browser }) => {
    token = await loginAndGetToken();
    snapshot = await getOidcConfig(token);
    await configureOidc(token);
    await clearOidcLink(token);

    const setupPage = await browser.newPage();
    try {
      await linkOidcViaBrowser(setupPage);
    } finally {
      await setupPage.close();
    }
  });

  test.afterAll(async () => {
    await clearOidcLink(token);
    await setOidcConfig(token, snapshot);
  });

  test('OIDC login button is visible after account linking', async ({
    page,
  }) => {
    await page.goto(`${TEST_CONFIG.appUrl}/auth/login`);

    const oidcButton = page.getByRole('button', { name: /sign in with/i });
    await expect(oidcButton).toBeVisible({ timeout: 10_000 });
    await expect(oidcButton).toContainText(TEST_CONFIG.oidcProviderName);
  });

  test('full OIDC login flow authenticates and redirects to dashboard', async ({
    page,
  }) => {
    await page.goto(`${TEST_CONFIG.appUrl}/auth/login`);

    await page.getByRole('button', { name: /sign in with/i }).click();

    await expect(page).toHaveURL(/localhost:8080/, { timeout: 10_000 });

    await page.locator('#username').waitFor({ state: 'visible', timeout: 5_000 });
    await page.locator('#username').fill(TEST_CONFIG.oidcUsername);
    await page.locator('#password').fill(TEST_CONFIG.oidcPassword);
    await page.locator('#kc-login').click();

    await expect(page).toHaveURL(/\/dashboard/, { timeout: 15_000 });

    await expect(page.locator('body')).not.toContainText('Sign In', {
      timeout: 5_000,
    });
  });
});
