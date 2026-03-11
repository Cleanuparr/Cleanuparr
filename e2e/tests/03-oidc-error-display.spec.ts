import { test, expect } from '@playwright/test';
import { TEST_CONFIG } from './helpers/test-config';

test.describe.serial('OIDC Error Display', () => {
  test('callback page shows error for missing code and redirects to login', async ({
    page,
  }) => {
    await page.goto(`${TEST_CONFIG.appUrl}/auth/oidc/callback`);

    await expect(page.locator('.oidc-callback__error')).toHaveText(
      'Invalid callback - missing authorization code',
    );
    await expect(page.locator('.oidc-callback__redirect')).toHaveText(
      'Redirecting to login...',
    );

    await expect(page).toHaveURL(/\/auth\/login/, { timeout: 5_000 });
  });

  test('callback page shows error for unauthorized', async ({ page }) => {
    await page.goto(
      `${TEST_CONFIG.appUrl}/auth/oidc/callback?oidc_error=unauthorized`,
    );

    await expect(page.locator('.oidc-callback__error')).toHaveText(
      'Your account is not authorized for OIDC login',
    );
  });

  test('callback page shows error for provider_error', async ({ page }) => {
    await page.goto(
      `${TEST_CONFIG.appUrl}/auth/oidc/callback?oidc_error=provider_error`,
    );

    await expect(page.locator('.oidc-callback__error')).toHaveText(
      'The identity provider returned an error',
    );
  });

  test('callback page shows error for exchange_failed', async ({ page }) => {
    await page.goto(
      `${TEST_CONFIG.appUrl}/auth/oidc/callback?oidc_error=exchange_failed`,
    );

    await expect(page.locator('.oidc-callback__error')).toHaveText(
      'Failed to complete sign in',
    );
  });

  test('callback page shows fallback error for unknown code', async ({
    page,
  }) => {
    await page.goto(
      `${TEST_CONFIG.appUrl}/auth/oidc/callback?oidc_error=xyz`,
    );

    await expect(page.locator('.oidc-callback__error')).toHaveText(
      'An unknown error occurred',
    );
  });

  test('login page shows error from oidc_error query param', async ({
    page,
  }) => {
    await page.goto(
      `${TEST_CONFIG.appUrl}/auth/login?oidc_error=unauthorized`,
    );

    await expect(page.locator('.error-message')).toHaveText(
      'Your account is not authorized for OIDC login',
    );
  });
});
