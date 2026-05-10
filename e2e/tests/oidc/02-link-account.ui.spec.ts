import { test, expect, TEST_CONFIG } from '../fixtures/base';

test.describe.serial('OIDC — account linking via settings', () => {
  test('authenticated user can link OIDC account', async ({ page }) => {
    await page.goto(`${TEST_CONFIG.appUrl}/auth/login`);
    await page.getByRole('textbox', { name: 'Username' }).fill(TEST_CONFIG.adminUsername);
    await page.getByRole('textbox', { name: 'Password' }).fill(TEST_CONFIG.adminPassword);
    await page.getByRole('button', { name: 'Sign In', exact: true }).click();
    await expect(page).toHaveURL(/\/dashboard/, { timeout: 10_000 });

    await page.goto(`${TEST_CONFIG.appUrl}/settings/account`);
    await expect(page).toHaveURL(/\/settings\/account/);

    await page.getByText('OIDC / SSO').click();

    const linkButton = page.getByRole('button', { name: /link account|re-link/i });
    await expect(linkButton).toBeVisible({ timeout: 5_000 });
    await linkButton.click();

    await expect(page).toHaveURL(/localhost:8080/, { timeout: 10_000 });

    await page.locator('#username').fill(TEST_CONFIG.oidcUsername);
    await page.locator('#password').fill(TEST_CONFIG.oidcPassword);
    await page.locator('#kc-login').click();

    await expect(page).toHaveURL(/settings\/account\?oidc_link=success/, { timeout: 15_000 });
  });
});
