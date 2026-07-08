import { test, expect } from '@playwright/test';
import { loginAndGotoSettings, selectOption } from '../helpers/ui';

// Behavior-parity spec for the Download Clients create/edit modal (client-type driven fields).
test.describe('Download Clients UI', () => {
  test('client type switching shows/hides fields and auto-fills URL base', async ({ page }) => {
    await loginAndGotoSettings(page, 'download-clients');
    await page.getByRole('button', { name: 'Add Client' }).click();
    const modal = page.getByRole('dialog', { name: 'Add Client' });
    await expect(modal).toBeVisible();

    const username = modal.locator('app-input').filter({ hasText: 'Username' });
    const urlBase = modal.locator('app-input').filter({ hasText: 'URL Base' }).locator('input');

    // qBittorrent (default) shows Username.
    await expect(username).toBeVisible();

    // Deluge hides Username.
    await selectOption(modal, 'Client Type', 'Deluge');
    await expect(username).toHaveCount(0);

    // Transmission auto-fills the URL base.
    await selectOption(modal, 'Client Type', 'Transmission');
    await expect(urlBase).toHaveValue('transmission');
    await expect(username).toBeVisible();

    // rTorrent auto-fills a different URL base.
    await selectOption(modal, 'Client Type', 'rTorrent');
    await expect(urlBase).toHaveValue('plugins/httprpc/action.php');
  });

  test('name and host required gate the modal Save button', async ({ page }) => {
    await loginAndGotoSettings(page, 'download-clients');
    await page.getByRole('button', { name: 'Add Client' }).click();
    const modal = page.getByRole('dialog', { name: 'Add Client' });

    const save = modal.getByRole('button', { name: 'Save' });
    await expect(save).toBeDisabled();

    await modal.locator('app-input').first().locator('input').fill('E2E Client'); // Name
    await expect(save).toBeDisabled();

    await modal.locator('app-input').filter({ hasText: 'Host' }).locator('input').fill('http://localhost:8090');
    await expect(save).toBeEnabled();
  });
});
