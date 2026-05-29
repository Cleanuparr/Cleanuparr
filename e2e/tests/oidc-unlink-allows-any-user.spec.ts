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
import {
  createKeycloakUser,
  deleteKeycloakUser,
} from './helpers/keycloak';

const ANOTHER_USER = 'anotheruser';
const ANOTHER_PASS = 'anotherpass';
const ANOTHER_EMAIL = 'anotheruser@example.com';

test.describe.serial('OIDC Unlink Allows Any User', () => {
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

    await createKeycloakUser(ANOTHER_USER, ANOTHER_PASS, ANOTHER_EMAIL);
  });

  test.afterAll(async () => {
    await deleteKeycloakUser(ANOTHER_USER);
    await clearOidcLink(adminToken);
    await setOidcConfig(adminToken, snapshot);
  });

  test('unlinking OIDC subject via UI succeeds', async ({ page }) => {
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

    await page.goto(`${TEST_CONFIG.appUrl}/settings/account`);
    await page.getByText('OIDC / SSO').click();

    const subjectEl = page.locator('.oidc-link-section__subject');
    await expect(subjectEl).toBeVisible({ timeout: 5_000 });

    const unlinkButton = page.getByRole('button', { name: 'Unlink' });
    await expect(unlinkButton).toBeVisible({ timeout: 5_000 });
    await unlinkButton.click();

    const confirmButton = page.getByRole('alertdialog').getByRole('button', { name: 'Unlink' });
    await expect(confirmButton).toBeVisible({ timeout: 5_000 });
    await confirmButton.click();

    await expect(page.getByText('OIDC account unlinked')).toBeVisible({
      timeout: 5_000,
    });

    await expect(subjectEl).not.toBeVisible({ timeout: 5_000 });

    const linkButton = page.getByRole('button', { name: 'Link Account' });
    await expect(linkButton).toBeVisible({ timeout: 5_000 });
  });

  test('OIDC login still works after unlinking', async ({ page }) => {
    await page.goto(`${TEST_CONFIG.appUrl}/auth/login`);

    await page.getByRole('button', { name: /sign in with/i }).click();

    await expect(page).toHaveURL(/localhost:8080/, { timeout: 10_000 });

    await page.locator('#username').waitFor({ state: 'visible', timeout: 5_000 });
    await page.locator('#username').fill(TEST_CONFIG.oidcUsername);
    await page.locator('#password').fill(TEST_CONFIG.oidcPassword);
    await page.locator('#kc-login').click();

    await expect(page).toHaveURL(/\/dashboard/, { timeout: 15_000 });
  });

  test('a different Keycloak user can also log in after unlinking', async ({
    page,
  }) => {
    await page.goto(`${TEST_CONFIG.appUrl}/auth/login`);

    await page.getByRole('button', { name: /sign in with/i }).click();

    await expect(page).toHaveURL(/localhost:8080/, { timeout: 10_000 });

    await page.locator('#username').waitFor({ state: 'visible', timeout: 5_000 });
    await page.locator('#username').fill(ANOTHER_USER);
    await page.locator('#password').fill(ANOTHER_PASS);
    await page.locator('#kc-login').click();

    await expect(page).toHaveURL(/\/dashboard/, { timeout: 15_000 });

    await expect(page.locator('body')).not.toContainText('Sign In', {
      timeout: 5_000,
    });
  });
});
