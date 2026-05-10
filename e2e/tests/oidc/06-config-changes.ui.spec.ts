<<<<<<<< HEAD:e2e/tests/oidc-config-changes.spec.ts
import { test, expect } from '@playwright/test';
import { TEST_CONFIG } from './helpers/test-config';
import {
  clearOidcLink,
  configureOidc,
  getOidcConfig,
  loginAndGetToken,
  OidcConfigSnapshot,
  setOidcConfig,
  updateOidcConfig,
} from './helpers/app-api';

test.describe.serial('OIDC Configuration Changes', () => {
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

  test('disabling OIDC hides the login button', async ({ page }) => {
    await updateOidcConfig(token, { enabled: false });
========
import { test, expect, TEST_CONFIG, CleanuparrApi } from '../fixtures/base';

async function adminApi(): Promise<CleanuparrApi> {
  const api = new CleanuparrApi();
  const tokens = await api.auth.loginAndCaptureTokens(
    TEST_CONFIG.adminUsername,
    TEST_CONFIG.adminPassword,
  );
  api.setToken(tokens.accessToken);
  return api;
}

test.describe.serial('OIDC — configuration changes', () => {
  test('disabling OIDC hides the login button', async ({ page }) => {
    const api = await adminApi();
    await api.account.patchOidcConfig({ enabled: false });
>>>>>>>> e131fe85 (migrated OIDC specs (00-09, 15) to tests/oidc/):e2e/tests/oidc/06-config-changes.ui.spec.ts

    await page.goto(`${TEST_CONFIG.appUrl}/auth/login`);

    const oidcButton = page.locator('.oidc-login-btn');
    await expect(oidcButton).not.toBeVisible({ timeout: 5_000 });

    const divider = page.locator('.divider');
    await expect(divider).not.toBeVisible();
  });

<<<<<<<< HEAD:e2e/tests/oidc-config-changes.spec.ts
  test('changing provider name updates the login button text', async ({
    page,
  }) => {
    await updateOidcConfig(token, {
      enabled: true,
      providerName: 'MyCustomIdP',
    });
========
  test('changing provider name updates the login button text', async ({ page }) => {
    const api = await adminApi();
    await api.account.patchOidcConfig({ enabled: true, providerName: 'MyCustomIdP' });
>>>>>>>> e131fe85 (migrated OIDC specs (00-09, 15) to tests/oidc/):e2e/tests/oidc/06-config-changes.ui.spec.ts

    await page.goto(`${TEST_CONFIG.appUrl}/auth/login`);

    const oidcButton = page.getByRole('button', { name: /sign in with/i });
    await expect(oidcButton).toBeVisible({ timeout: 10_000 });
    await expect(oidcButton).toContainText('MyCustomIdP');
  });

<<<<<<<< HEAD:e2e/tests/oidc-config-changes.spec.ts
  test('re-enabling with original provider name restores the button', async ({
    page,
  }) => {
    await updateOidcConfig(token, {
========
  test('re-enabling with original provider name restores the button', async ({ page }) => {
    const api = await adminApi();
    await api.account.patchOidcConfig({
>>>>>>>> e131fe85 (migrated OIDC specs (00-09, 15) to tests/oidc/):e2e/tests/oidc/06-config-changes.ui.spec.ts
      enabled: true,
      providerName: TEST_CONFIG.oidcProviderName,
    });

    await page.goto(`${TEST_CONFIG.appUrl}/auth/login`);

    const oidcButton = page.getByRole('button', { name: /sign in with/i });
    await expect(oidcButton).toBeVisible({ timeout: 10_000 });
    await expect(oidcButton).toContainText(TEST_CONFIG.oidcProviderName);
  });
});
