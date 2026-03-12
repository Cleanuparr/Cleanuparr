import { test, expect } from '@playwright/test';
import { TEST_CONFIG } from './helpers/test-config';
import { loginAndGetToken } from './helpers/app-api';
import {
  createKeycloakUser,
  deleteKeycloakUser,
} from './helpers/keycloak';

const ANOTHER_USER = 'anotheruser';
const ANOTHER_PASS = 'anotherpass';
const ANOTHER_EMAIL = 'anotheruser@example.com';

const API = TEST_CONFIG.appUrl;

test.describe.serial('OIDC Unlink Allows Any User', () => {
  let adminToken: string;

  test.beforeAll(async () => {
    adminToken = await loginAndGetToken();
    await createKeycloakUser(ANOTHER_USER, ANOTHER_PASS, ANOTHER_EMAIL);
  });

  test.afterAll(async () => {
    await deleteKeycloakUser(ANOTHER_USER);
  });

  test('unlinking OIDC subject via UI succeeds', async ({ page }) => {
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

    // Navigate to account settings and expand OIDC card
    await page.goto(`${TEST_CONFIG.appUrl}/settings/account`);
    await page.getByText('OIDC / SSO').click();

    // Verify subject is currently linked
    const subjectEl = page.locator('.oidc-link-section__subject');
    await expect(subjectEl).toBeVisible({ timeout: 5_000 });

    // Click the Unlink button
    const unlinkButton = page.getByRole('button', { name: 'Unlink' });
    await expect(unlinkButton).toBeVisible({ timeout: 5_000 });
    await unlinkButton.click();

    // Confirm the destructive dialog
    const confirmButton = page.getByRole('alertdialog').getByRole('button', { name: 'Unlink' });
    await expect(confirmButton).toBeVisible({ timeout: 5_000 });
    await confirmButton.click();

    // Verify success toast
    await expect(page.getByText('OIDC account unlinked')).toBeVisible({
      timeout: 5_000,
    });

    // Subject should no longer be displayed
    await expect(subjectEl).not.toBeVisible({ timeout: 5_000 });

    // Button should now say "Link Account" instead of "Re-link"
    const linkButton = page.getByRole('button', { name: 'Link Account' });
    await expect(linkButton).toBeVisible({ timeout: 5_000 });
  });

  test('OIDC login still works after unlinking', async ({ page }) => {
    await page.goto(`${TEST_CONFIG.appUrl}/auth/login`);

    // Button should still be visible (OIDC is configured, just no linked subject)
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

    // Should succeed — no subject restriction when unlinked
    await expect(page).toHaveURL(/\/dashboard/, { timeout: 15_000 });

    await expect(page.locator('body')).not.toContainText('Sign In', {
      timeout: 5_000,
    });
  });
});
