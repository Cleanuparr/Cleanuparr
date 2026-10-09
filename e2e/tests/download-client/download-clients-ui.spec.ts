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

  test('SABnzbd shows only the API key field', async ({ page }) => {
    await loginAndGotoSettings(page, 'download-clients');
    await page.getByRole('button', { name: 'Add Client' }).click();
    const modal = page.getByRole('dialog', { name: 'Add Client' });
    await expect(modal).toBeVisible();

    await selectOption(modal, 'Client Type', 'SABnzbd');

    await expect(modal.locator('app-input').filter({ hasText: 'Username' })).toHaveCount(0);
    await expect(modal.locator('app-input').filter({ hasText: 'Password' })).toHaveCount(0);
    await expect(modal.locator('app-input').filter({ hasText: 'API Key' })).toBeVisible();

    const save = modal.getByRole('button', { name: 'Save' });
    await modal.locator('app-input').first().locator('input').fill('E2E SAB Client'); // Name
    await modal.locator('app-input').filter({ hasText: 'Host' }).locator('input').fill('http://localhost:8090');
    await expect(save).toBeDisabled();

    await modal.locator('app-input').filter({ hasText: 'API Key' }).locator('input').fill('sab-api-key');
    await expect(save).toBeEnabled();
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

  test('a failed types load shows the error state and recovers on retry', async ({ page }) => {
    await page.route('**/download_client/types', (route) => route.abort());
    await loginAndGotoSettings(page, 'download-clients');

    await expect(page.getByRole('heading', { name: 'Could not connect to server' })).toBeVisible();

    await page.unroute('**/download_client/types');
    await page.getByRole('button', { name: 'Retry' }).click();

    await expect(page.getByRole('button', { name: 'Add Client' })).toBeVisible();
  });
});
