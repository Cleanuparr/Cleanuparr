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
import { linkOidcViaBrowser } from './helpers/oidc';

const API = TEST_CONFIG.appUrl;

test.describe.serial('OIDC Exclusive Mode', () => {
  // Token obtained BEFORE enabling exclusive mode (password login will be blocked)
  let adminToken: string;
  let snapshot: OidcConfigSnapshot;

  test.beforeAll(async ({ browser }) => {
    adminToken = await loginAndGetToken();
    snapshot = await getOidcConfig(adminToken);
    await configureOidc(adminToken);
    await clearOidcLink(adminToken);

    const setupPage = await browser.newPage();
    try {
      await linkOidcViaBrowser(setupPage);
    } finally {
      await setupPage.close();
    }

    await updateOidcConfig(adminToken, { exclusiveMode: true });
  });

  test.afterAll(async () => {
    try {
      await updateOidcConfig(adminToken, { exclusiveMode: false });
    } catch {
      // best effort — snapshot restore below will fix it anyway
    }
    await clearOidcLink(adminToken);
    await setOidcConfig(adminToken, snapshot);
  });

  test('login page shows only OIDC button when exclusive mode is active', async ({
    page,
  }) => {
    await page.goto(`${TEST_CONFIG.appUrl}/auth/login`);

    const oidcButton = page.locator('.oidc-login-btn');
    await expect(oidcButton).toBeVisible({ timeout: 10_000 });
    await expect(oidcButton).toContainText(TEST_CONFIG.oidcProviderName);

    const loginForm = page.locator('.login-form');
    await expect(loginForm).not.toBeVisible();

    const divider = page.locator('.divider');
    await expect(divider).not.toBeVisible();

    const plexButton = page.locator('.plex-login-btn');
    await expect(plexButton).not.toBeVisible();
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

    await expect(page.locator('body')).not.toContainText('Sign In', {
      timeout: 5_000,
    });
  });

  test('password login API returns 403 in exclusive mode', async () => {
    const res = await fetch(`${API}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        username: TEST_CONFIG.adminUsername,
        password: TEST_CONFIG.adminPassword,
      }),
    });

    expect(res.status).toBe(403);
  });

  test('auth status API reflects exclusive mode', async () => {
    const res = await fetch(`${API}/api/auth/status`);
    expect(res.ok).toBe(true);

    const data = await res.json();
    expect(data.oidcExclusiveMode).toBe(true);
  });

  test('settings page shows warning notices and disabled controls', async ({
    page,
  }) => {
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
    const exclusiveToggle = page.getByText('Exclusive Mode', { exact: true });
    await expect(exclusiveToggle).toBeVisible({ timeout: 5_000 });
  });

  test('disabling exclusive mode restores credential form on login page', async ({
    page,
  }) => {
    await updateOidcConfig(adminToken, { exclusiveMode: false });

    await page.goto(`${TEST_CONFIG.appUrl}/auth/login`);

    const loginForm = page.locator('.login-form');
    await expect(loginForm).toBeVisible({ timeout: 10_000 });

    const oidcButton = page.locator('.oidc-login-btn');
    await expect(oidcButton).toBeVisible();

    const divider = page.locator('.divider');
    await expect(divider).toBeVisible();
  });

  test('password login works again after disabling exclusive mode', async ({
    page,
  }) => {
    await page.goto(`${TEST_CONFIG.appUrl}/auth/login`);

    await page
      .getByRole('textbox', { name: 'Username' })
      .fill(TEST_CONFIG.adminUsername);
    await page
      .getByRole('textbox', { name: 'Password' })
      .fill(TEST_CONFIG.adminPassword);
    await page
      .getByRole('button', { name: 'Sign In', exact: true })
      .click();

    await expect(page).toHaveURL(/\/dashboard/, { timeout: 10_000 });
  });
});
