import { test, expect } from '@playwright/test';
import { TEST_CONFIG } from './helpers/test-config';

test.describe.serial('OIDC Login Persistence', () => {
  test('OIDC login still works after configuration changes', async ({
    page,
  }) => {
    await page.goto(`${TEST_CONFIG.appUrl}/auth/login`);

    const oidcButton = page.getByRole('button', { name: /sign in with/i });
    await expect(oidcButton).toBeVisible({ timeout: 10_000 });
    await expect(oidcButton).toContainText(TEST_CONFIG.oidcProviderName);

    await oidcButton.click();

    // Should redirect to Keycloak
    await expect(page).toHaveURL(/localhost:8080/, { timeout: 10_000 });

    // Fill Keycloak login form (each test gets a fresh browser context, so always required)
    await page.locator('#username').waitFor({ state: 'visible', timeout: 5_000 });
    await page.locator('#username').fill(TEST_CONFIG.oidcUsername);
    await page.locator('#password').fill(TEST_CONFIG.oidcPassword);
    await page.locator('#kc-login').click();

    // Full flow: Keycloak → /api/auth/oidc/callback → /auth/oidc/callback?code=... → /dashboard
    await expect(page).toHaveURL(/\/dashboard/, { timeout: 15_000 });

    // Verify we're authenticated
    await expect(page.locator('body')).not.toContainText('Sign In', {
      timeout: 5_000,
    });
  });
});
