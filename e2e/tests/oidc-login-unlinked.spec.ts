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

test.describe('OIDC Login Without Linked Subject', () => {
  let token: string;
  let snapshot: OidcConfigSnapshot;

  test.beforeAll(async () => {
    token = await loginAndGetToken();
    snapshot = await getOidcConfig(token);
    await configureOidc(token);
    await clearOidcLink(token);
  });

  test.afterAll(async () => {
    await clearOidcLink(token);
    await setOidcConfig(token, snapshot);
  });

  test('OIDC button is visible without a linked subject', async ({ page }) => {
    await page.goto(`${TEST_CONFIG.appUrl}/auth/login`);

    const oidcButton = page.getByRole('button', { name: /sign in with/i });
    await expect(oidcButton).toBeVisible({ timeout: 10_000 });
    await expect(oidcButton).toContainText(TEST_CONFIG.oidcProviderName);
  });

  test('OIDC login works without a linked subject', async ({ page }) => {
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
