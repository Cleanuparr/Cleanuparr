import { test, expect } from '@playwright/test';
import { TEST_CONFIG } from './helpers/test-config';

test.describe.serial('OIDC Account Linking', () => {
  test('authenticated user can link OIDC account via settings', async ({
    page,
  }) => {
    // Log in with local credentials
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

    // Navigate to settings and expand the OIDC accordion
    await page.goto(`${TEST_CONFIG.appUrl}/settings/account`);
    await expect(page).toHaveURL(/\/settings\/account/);

    await page.getByText('OIDC / SSO').click();

    const linkButton = page.getByRole('button', { name: /link account|re-link/i });
    await expect(linkButton).toBeVisible({ timeout: 5_000 });
    await linkButton.click();

    // Should redirect to Keycloak
    await expect(page).toHaveURL(/localhost:8080/, { timeout: 10_000 });

    // Fill Keycloak login form
    await page.locator('#username').fill(TEST_CONFIG.oidcUsername);
    await page.locator('#password').fill(TEST_CONFIG.oidcPassword);
    await page.locator('#kc-login').click();

    // Should redirect back to settings with success
    await expect(page).toHaveURL(/settings\/account\?oidc_link=success/, {
      timeout: 15_000,
    });
  });
});
