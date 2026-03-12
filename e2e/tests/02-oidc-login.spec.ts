import { test, expect } from '@playwright/test';
import { TEST_CONFIG } from './helpers/test-config';

test.describe.serial('OIDC Login', () => {
  test('OIDC login button is visible after account linking', async ({
    page,
  }) => {
    await page.goto(`${TEST_CONFIG.appUrl}/auth/login`);

    // The button should now be visible since AuthorizedSubject was set by the link test
    const oidcButton = page.getByRole('button', { name: /sign in with/i });
    await expect(oidcButton).toBeVisible({ timeout: 10_000 });
    await expect(oidcButton).toContainText(TEST_CONFIG.oidcProviderName);
  });

  test('full OIDC login flow authenticates and redirects to dashboard', async ({
    page,
  }) => {
    await page.goto(`${TEST_CONFIG.appUrl}/auth/login`);

    await page.getByRole('button', { name: /sign in with/i }).click();

    // Should redirect to Keycloak
    await expect(page).toHaveURL(/localhost:8080/, { timeout: 10_000 });

    // Fill Keycloak login form (each test gets a fresh browser context, so always required)
    await page.locator('#username').waitFor({ state: 'visible', timeout: 5_000 });
    await page.locator('#username').fill(TEST_CONFIG.oidcUsername);
    await page.locator('#password').fill(TEST_CONFIG.oidcPassword);
    await page.locator('#kc-login').click();

    // Full flow: Keycloak → /api/auth/oidc/callback → /auth/oidc/callback?code=... → /dashboard
    await expect(page).toHaveURL(/\/dashboard/, { timeout: 15_000 });

    // Verify we're authenticated — dashboard content visible, not redirected to login
    await expect(page.locator('body')).not.toContainText('Sign In', {
      timeout: 5_000,
    });
  });
});
