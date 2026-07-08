import { test, expect } from '../fixtures/base';

test.describe('General settings — UI smoke', () => {
  test('settings page loads for authenticated admin', async ({ authenticatedPage }) => {
    await authenticatedPage.goto('/settings/general');
    await expect(authenticatedPage).toHaveURL(/\/settings\/general/);
    await expect(authenticatedPage.locator('body')).toBeVisible();
  });

  test('unauthenticated visit redirects to login', async ({ page }) => {
    await page.goto('/settings/general');
    await expect(page).toHaveURL(/(login|auth)/);
  });
});
