import { expect, Page } from '@playwright/test';
import { TEST_CONFIG } from './test-config';

/**
 * Drives the browser through the OIDC link flow:
 * local login → settings/account → Link Account → Keycloak login → callback success.
 *
 * Leaves the page on /settings/account?oidc_link=success.
 */
export async function linkOidcViaBrowser(page: Page): Promise<void> {
  await page.goto(`${TEST_CONFIG.appUrl}/auth/login`);

  await page
    .getByRole('textbox', { name: 'Username' })
    .fill(TEST_CONFIG.adminUsername);
  await page
    .getByRole('textbox', { name: 'Password' })
    .fill(TEST_CONFIG.adminPassword);
  await page.getByRole('button', { name: 'Sign In', exact: true }).click();

  await expect(page).toHaveURL(/\/dashboard/, { timeout: 10_000 });

  await page.goto(`${TEST_CONFIG.appUrl}/settings/account`);
  await page.getByText('OIDC / SSO').click();

  const linkButton = page.getByRole('button', { name: /link account|re-link/i });
  await expect(linkButton).toBeVisible({ timeout: 5_000 });
  await linkButton.click();

  await expect(page).toHaveURL(/localhost:8080/, { timeout: 10_000 });

  await page.locator('#username').waitFor({ state: 'visible', timeout: 5_000 });
  await page.locator('#username').fill(TEST_CONFIG.oidcUsername);
  await page.locator('#password').fill(TEST_CONFIG.oidcPassword);
  await page.locator('#kc-login').click();

  await expect(page).toHaveURL(/settings\/account\?oidc_link=success/, {
    timeout: 15_000,
  });
}
