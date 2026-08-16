import { test, expect } from '@playwright/test';
import { TEST_CONFIG } from '../helpers/test-config';

test.describe('Login lockout countdown', () => {
  test('a rejected password counts the lockout down in the login form', async ({ page }) => {
    await page.goto(`${TEST_CONFIG.appUrl}/auth/login`);

    const signIn = page.getByRole('button', { name: 'Sign In', exact: true });
    const countdown = page.locator('.retry-countdown');

    await page.getByRole('textbox', { name: 'Username' }).fill(TEST_CONFIG.adminUsername);
    await page.getByRole('textbox', { name: 'Password' }).fill('wrong-password');
    await signIn.click();

    // The second failure buys a four second window to assert against.
    await expect(countdown).toBeHidden({ timeout: 15_000 });
    await signIn.click();

    await expect(countdown).toHaveText(/Try again in [1-9]\d*s/);
    await expect(signIn).toBeDisabled();

    await expect(countdown).toBeHidden({ timeout: 15_000 });
    await expect(signIn).toBeEnabled();

    await page.getByRole('textbox', { name: 'Password' }).fill(TEST_CONFIG.adminPassword);
    await signIn.click();
    await expect(page).toHaveURL(/\/dashboard/, { timeout: 15_000 });
  });
});
