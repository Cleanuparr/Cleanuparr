import { test, expect, TEST_CONFIG } from '../fixtures/base';
import { getSubjectForUser } from '../helpers/keycloak';

test.describe.serial('OIDC — settings UI', () => {
  async function loginAsAdmin(page: import('@playwright/test').Page): Promise<void> {
    await page.goto(`${TEST_CONFIG.appUrl}/auth/login`);
    await page.getByRole('textbox', { name: 'Username' }).fill(TEST_CONFIG.adminUsername);
    await page.getByRole('textbox', { name: 'Password' }).fill(TEST_CONFIG.adminPassword);
    await page.getByRole('button', { name: 'Sign In', exact: true }).click();
    await expect(page).toHaveURL(/\/dashboard/, { timeout: 10_000 });
  }

  test('settings page shows linked OIDC subject', async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto(`${TEST_CONFIG.appUrl}/settings/account`);
    await page.getByText('OIDC / SSO').click();

    const subjectEl = page.locator('.oidc-link-section__subject');
    await expect(subjectEl).toBeVisible({ timeout: 5_000 });

    const expectedSubject = await getSubjectForUser(TEST_CONFIG.oidcUsername);
    await expect(subjectEl).toHaveText(expectedSubject);
  });

  test('settings page shows Re-link button when account is linked', async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto(`${TEST_CONFIG.appUrl}/settings/account`);
    await page.getByText('OIDC / SSO').click();

    const relinkButton = page.getByRole('button', { name: 'Re-link' });
    await expect(relinkButton).toBeVisible({ timeout: 5_000 });
  });

  test('oidc_link=success query param shows toast and expands accordion', async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto(`${TEST_CONFIG.appUrl}/settings/account?oidc_link=success`);

    await expect(page.getByText('OIDC account linked successfully')).toBeVisible({ timeout: 5_000 });
    await expect(page.locator('.oidc-link-section__subject')).toBeVisible({ timeout: 5_000 });
  });

  test('oidc_link_error query param shows toast and expands accordion', async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto(`${TEST_CONFIG.appUrl}/settings/account?oidc_link_error=failed`);

    await expect(page.getByText('Failed to link OIDC account')).toBeVisible({ timeout: 5_000 });
    await expect(page.locator('.oidc-link-section__subject')).toBeVisible({ timeout: 5_000 });
  });
});
