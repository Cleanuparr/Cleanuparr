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

    await page.goto(`${TEST_CONFIG.appUrl}/auth/login`);

    const oidcButton = page.locator('.oidc-login-btn');
    await expect(oidcButton).not.toBeVisible({ timeout: 5_000 });

    const divider = page.locator('.divider');
    await expect(divider).not.toBeVisible();
  });

  test('changing provider name updates the login button text', async ({ page }) => {
    const api = await adminApi();
    await api.account.patchOidcConfig({ enabled: true, providerName: 'MyCustomIdP' });

    await page.goto(`${TEST_CONFIG.appUrl}/auth/login`);

    const oidcButton = page.getByRole('button', { name: /sign in with/i });
    await expect(oidcButton).toBeVisible({ timeout: 10_000 });
    await expect(oidcButton).toContainText('MyCustomIdP');
  });

  test('re-enabling with original provider name restores the button', async ({ page }) => {
    const api = await adminApi();
    await api.account.patchOidcConfig({
      enabled: true,
      providerName: TEST_CONFIG.oidcProviderName,
    });

    await page.goto(`${TEST_CONFIG.appUrl}/auth/login`);

    const oidcButton = page.getByRole('button', { name: /sign in with/i });
    await expect(oidcButton).toBeVisible({ timeout: 10_000 });
    await expect(oidcButton).toContainText(TEST_CONFIG.oidcProviderName);
  });
});
