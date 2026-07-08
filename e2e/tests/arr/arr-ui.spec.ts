import { test, expect } from '@playwright/test';
import { loginAndGotoSettings } from '../helpers/ui';

// Behavior-parity spec for the Arr (Sonarr) instance create/edit modal.
test.describe('Arr Settings UI', () => {
  test('required fields gate the modal Save button', async ({ page }) => {
    await loginAndGotoSettings(page, 'arr/sonarr');
    await page.getByRole('button', { name: 'Add Instance' }).click();
    const modal = page.getByRole('dialog', { name: 'Add Instance' });
    await expect(modal).toBeVisible();

    const save = modal.getByRole('button', { name: 'Save' });
    await expect(save).toBeDisabled();

    await modal.locator('app-input').first().locator('input').fill('E2E Sonarr'); // Name
    await modal.locator('app-input').filter({ hasText: 'URL' }).first().locator('input').fill('http://localhost:9999');
    await expect(save).toBeDisabled();

    await modal.locator('app-input').filter({ hasText: 'API Key' }).locator('input').fill('0123456789abcdef0123456789abcdef');
    await expect(save).toBeEnabled();
  });

  test('create then delete an instance round-trip', async ({ page }) => {
    await loginAndGotoSettings(page, 'arr/sonarr');
    await page.getByRole('button', { name: 'Add Instance' }).click();
    const modal = page.getByRole('dialog', { name: 'Add Instance' });

    await modal.locator('app-input').first().locator('input').fill('E2E Sonarr RT');
    await modal.locator('app-input').filter({ hasText: 'URL' }).first().locator('input').fill('http://localhost:9999');
    await modal.locator('app-input').filter({ hasText: 'API Key' }).locator('input').fill('0123456789abcdef0123456789abcdef');
    await modal.getByRole('button', { name: 'Save' }).click();

    const row = page.locator('.instance-row').filter({ hasText: 'E2E Sonarr RT' });
    await expect(row).toBeVisible({ timeout: 10_000 });

    await row.getByRole('button', { name: 'Delete' }).click();
    const dialog = page.getByRole('alertdialog', { name: 'Delete Instance' });
    await expect(dialog).toBeVisible();
    await dialog.getByRole('button', { name: 'Delete', exact: true }).click();
    await expect(row).toHaveCount(0, { timeout: 10_000 });
  });
});
