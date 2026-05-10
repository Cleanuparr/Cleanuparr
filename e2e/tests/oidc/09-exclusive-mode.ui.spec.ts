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

test.describe.serial('OIDC — exclusive mode', () => {
  test.beforeAll(async () => {
    const api = await adminApi();
    await api.account.patchOidcConfig({ exclusiveMode: true });
  });

  test.afterAll(async () => {
    try {
      const api = await adminApi();
      await api.account.patchOidcConfig({ exclusiveMode: false });
    } catch {
      // best-effort cleanup
    }
  });

  test('login page shows only OIDC button when exclusive mode is active', async ({ page }) => {
    await page.goto(`${TEST_CONFIG.appUrl}/auth/login`);

    const oidcButton = page.locator('.oidc-login-btn');
    await expect(oidcButton).toBeVisible({ timeout: 10_000 });
    await expect(oidcButton).toContainText(TEST_CONFIG.oidcProviderName);

    await expect(page.locator('.login-form')).not.toBeVisible();
    await expect(page.locator('.divider')).not.toBeVisible();
    await expect(page.locator('.plex-login-btn')).not.toBeVisible();
  });

  test('OIDC login still works in exclusive mode', async ({ page }) => {
    await page.goto(`${TEST_CONFIG.appUrl}/auth/login`);
    await page.locator('.oidc-login-btn').click();
    await expect(page).toHaveURL(/localhost:8080/, { timeout: 10_000 });

    await page.locator('#username').waitFor({ state: 'visible', timeout: 5_000 });
    await page.locator('#username').fill(TEST_CONFIG.oidcUsername);
    await page.locator('#password').fill(TEST_CONFIG.oidcPassword);
    await page.locator('#kc-login').click();

    await expect(page).toHaveURL(/\/dashboard/, { timeout: 15_000 });
    await expect(page.locator('body')).not.toContainText('Sign In', { timeout: 5_000 });
  });

  test('password login API returns 403 in exclusive mode', async ({ anonymousApi }) => {
    const res = await anonymousApi.auth.login(TEST_CONFIG.adminUsername, TEST_CONFIG.adminPassword);
    expect(res.status).toBe(403);
  });

  test('auth status API reflects exclusive mode', async ({ anonymousApi }) => {
    const res = await anonymousApi.auth.status();
    expect(res.ok).toBe(true);
    const data = await res.json();
    expect(data.oidcExclusiveMode).toBe(true);
  });

  test('settings page shows warning notices and disabled controls', async ({ page }) => {
    await page.goto(`${TEST_CONFIG.appUrl}/auth/login`);
    await page.locator('.oidc-login-btn').click();
    await expect(page).toHaveURL(/localhost:8080/, { timeout: 10_000 });
    await page.locator('#username').waitFor({ state: 'visible', timeout: 5_000 });
    await page.locator('#username').fill(TEST_CONFIG.oidcUsername);
    await page.locator('#password').fill(TEST_CONFIG.oidcPassword);
    await page.locator('#kc-login').click();
    await expect(page).toHaveURL(/\/dashboard/, { timeout: 15_000 });

    await page.goto(`${TEST_CONFIG.appUrl}/settings/account`);

    await expect(
      page.getByText('Password login is disabled while OIDC exclusive mode is active.'),
    ).toBeVisible({ timeout: 5_000 });
    await expect(
      page.getByText('Plex login is disabled while OIDC exclusive mode is active.'),
    ).toBeVisible({ timeout: 5_000 });

    await page.getByText('OIDC / SSO').click();
    await expect(page.getByText('Exclusive Mode', { exact: true })).toBeVisible({ timeout: 5_000 });
  });

  test('disabling exclusive mode restores credential form on login page', async ({ page }) => {
    const api = await adminApi();
    await api.account.patchOidcConfig({ exclusiveMode: false });

    await page.goto(`${TEST_CONFIG.appUrl}/auth/login`);

    await expect(page.locator('.login-form')).toBeVisible({ timeout: 10_000 });
    await expect(page.locator('.oidc-login-btn')).toBeVisible();
    await expect(page.locator('.divider')).toBeVisible();
  });

  test('password login works again after disabling exclusive mode', async ({ page }) => {
    await page.goto(`${TEST_CONFIG.appUrl}/auth/login`);

    await page.getByRole('textbox', { name: 'Username' }).fill(TEST_CONFIG.adminUsername);
    await page.getByRole('textbox', { name: 'Password' }).fill(TEST_CONFIG.adminPassword);
    await page.getByRole('button', { name: 'Sign In', exact: true }).click();
    await expect(page).toHaveURL(/\/dashboard/, { timeout: 10_000 });
  });
});
