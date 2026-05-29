import { test, expect, Page } from '@playwright/test';
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

const API = TEST_CONFIG.appUrl;

// UX hardening for the OIDC "no linked subject" trust mode
// The unlinked mode is intentional

test.describe.serial('OIDC Save without Link Warning', () => {
  let adminToken: string;
  let snapshot: OidcConfigSnapshot;

  test.beforeAll(async () => {
    adminToken = await loginAndGetToken();
    snapshot = await getOidcConfig(adminToken);
    await configureOidc(adminToken);
    await clearOidcLink(adminToken);
  });

  test.afterAll(async () => {
    await clearOidcLink(adminToken);
    await setOidcConfig(adminToken, snapshot);
  });

  async function loginUI(page: Page) {
    await page.goto(`${API}/auth/login`);
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
  }

  async function openOidcSettings(page: Page) {
    await page.goto(`${API}/settings/account`);
    await page.getByText('OIDC / SSO').click();
    await expect(page.getByRole('button', { name: 'Save OIDC Settings' })).toBeVisible({
      timeout: 5_000,
    });
  }

  test('Saving with Enabled=true and no linked subject shows the warning dialog', async ({
    page,
  }) => {
    await loginUI(page);
    await openOidcSettings(page);

    await expect(page.locator('.oidc-link-section__subject')).not.toBeVisible();

    await page.getByRole('button', { name: 'Save OIDC Settings' }).click();

    const dialog = page.getByRole('alertdialog', { name: 'Enable OIDC without a linked account' });
    await expect(dialog).toBeVisible({ timeout: 5_000 });
    await expect(dialog).toContainText('Enable OIDC without a linked account');
    await expect(dialog).toContainText('UNSAFE');
    await expect(
      dialog.getByRole('button', { name: 'Enable anyway' }),
    ).toBeVisible();

    await dialog.getByRole('button', { name: 'Cancel' }).click();
    await expect(dialog).not.toBeVisible({ timeout: 5_000 });
  });

  test('Cancelling the warning does not call the save API', async ({ page }) => {
    let putRequested = false;
    page.on('request', (req) => {
      if (req.method() === 'PUT' && req.url().endsWith('/api/account/oidc')) {
        putRequested = true;
      }
    });

    await loginUI(page);
    await openOidcSettings(page);

    await page.getByRole('button', { name: 'Save OIDC Settings' }).click();
    const dialog = page.getByRole('alertdialog', { name: 'Enable OIDC without a linked account' });
    await expect(dialog).toBeVisible({ timeout: 5_000 });
    await dialog.getByRole('button', { name: 'Cancel' }).click();
    await expect(dialog).not.toBeVisible({ timeout: 5_000 });

    expect(putRequested).toBe(false);

    await expect(page.getByText('OIDC settings saved')).not.toBeVisible();
  });

  test('Confirming the warning saves successfully', async ({ page }) => {
    await loginUI(page);
    await openOidcSettings(page);

    await page.getByRole('button', { name: 'Save OIDC Settings' }).click();
    const dialog = page.getByRole('alertdialog', { name: 'Enable OIDC without a linked account' });
    await expect(dialog).toBeVisible({ timeout: 5_000 });
    await dialog.getByRole('button', { name: 'Enable anyway' }).click();
    await expect(dialog).not.toBeVisible({ timeout: 5_000 });

    await expect(page.getByText('OIDC settings saved')).toBeVisible({
      timeout: 5_000,
    });
  });

  test('Saving with Enabled=false does not show the warning', async ({ page }) => {
    await loginUI(page);
    await openOidcSettings(page);

    await page.getByRole('switch', { name: 'Enable OIDC' }).click();

    await page.getByRole('button', { name: 'Save OIDC Settings' }).click();

    await expect(page.getByRole('alertdialog', { name: 'Enable OIDC without a linked account' })).not.toBeVisible({ timeout: 1_000 });

    await expect(page.getByText('OIDC settings saved')).toBeVisible({
      timeout: 5_000,
    });

    await updateOidcConfig(adminToken, { enabled: true });
  });

  test('Saving with a linked subject does not show the warning', async ({
    page,
  }) => {
    await loginUI(page);
    await page.goto(`${API}/settings/account`);
    await page.getByText('OIDC / SSO').click();

    const linkButton = page.getByRole('button', { name: 'Link Account' });
    await expect(linkButton).toBeVisible({ timeout: 5_000 });
    await linkButton.click();

    await expect(page).toHaveURL(/localhost:8080/, { timeout: 10_000 });
    await page.locator('#username').waitFor({ state: 'visible', timeout: 5_000 });
    await page.locator('#username').fill(TEST_CONFIG.oidcUsername);
    await page.locator('#password').fill(TEST_CONFIG.oidcPassword);
    await page.locator('#kc-login').click();

    await expect(page).toHaveURL(/\/settings\/account/, { timeout: 15_000 });
    await expect(page.locator('.oidc-link-section__subject')).toBeVisible({
      timeout: 5_000,
    });

    await page.getByRole('button', { name: 'Save OIDC Settings' }).click();
    await expect(page.getByRole('alertdialog', { name: 'Enable OIDC without a linked account' })).not.toBeVisible({ timeout: 1_000 });
    await expect(page.getByText('OIDC settings saved')).toBeVisible({
      timeout: 5_000,
    });
  });
});
