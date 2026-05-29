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
import { getSubjectForUser } from './helpers/keycloak';

test.describe('OIDC Settings UI', () => {
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

  async function loginAndGoToSettings(page: import('@playwright/test').Page) {
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
  }

  test('settings page shows linked OIDC subject', async ({ page }) => {
    await loginAndGoToSettings(page);
    await page.goto(`${TEST_CONFIG.appUrl}/settings/account`);

    await page.getByText('OIDC / SSO').click();

    const subjectEl = page.locator('.oidc-link-section__subject');
    await expect(subjectEl).toBeVisible({ timeout: 5_000 });

    const expectedSubject = await getSubjectForUser(TEST_CONFIG.oidcUsername);
    await expect(subjectEl).toHaveText(expectedSubject);
  });

  test('settings page shows Re-link button when account is linked', async ({
    page,
  }) => {
    await loginAndGoToSettings(page);
    await page.goto(`${TEST_CONFIG.appUrl}/settings/account`);

    await page.getByText('OIDC / SSO').click();

    const relinkButton = page.getByRole('button', { name: 'Re-link' });
    await expect(relinkButton).toBeVisible({ timeout: 5_000 });
  });

  test('oidc_link=success query param shows success toast and expands accordion', async ({
    page,
  }) => {
    await loginAndGoToSettings(page);
    await page.goto(
      `${TEST_CONFIG.appUrl}/settings/account?oidc_link=success`,
    );

    await expect(page.getByText('OIDC account linked successfully')).toBeVisible({
      timeout: 5_000,
    });

    const subjectEl = page.locator('.oidc-link-section__subject');
    await expect(subjectEl).toBeVisible({ timeout: 5_000 });
  });

  test('oidc_link_error query param shows error toast and expands accordion', async ({
    page,
  }) => {
    await loginAndGoToSettings(page);
    await page.goto(
      `${TEST_CONFIG.appUrl}/settings/account?oidc_link_error=failed`,
    );

    await expect(page.getByText('Failed to link OIDC account')).toBeVisible({
      timeout: 5_000,
    });

    const subjectEl = page.locator('.oidc-link-section__subject');
    await expect(subjectEl).toBeVisible({ timeout: 5_000 });
  });
});
