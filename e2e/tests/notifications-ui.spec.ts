import { test, expect, type Page } from '@playwright/test';
import { loginAndGotoSettings, selectOption } from './helpers/ui';

// Behavior-parity spec for the Notifications provider modal (discriminated union over provider type).
test.describe('Notifications UI', () => {
  async function openProvider(page: Page, providerName: string) {
    await page.getByRole('button', { name: 'Add Provider' }).click();
    const selection = page.getByRole('dialog', { name: 'Add Notification Provider' });
    await expect(selection).toBeVisible();
    await selection.locator('button.provider-card').filter({ hasText: providerName }).click();
    const modal = page.getByRole('dialog', { name: `Add ${providerName} Provider` });
    await expect(modal).toBeVisible();
    return modal;
  }

  test('Discord modal shows webhook field and gates Save on required fields', async ({ page }) => {
    await loginAndGotoSettings(page, 'notifications');
    const modal = await openProvider(page, 'Discord');

    const webhook = modal.locator('app-input').filter({ hasText: 'Webhook URL' });
    await expect(webhook).toBeVisible();

    const save = modal.getByRole('button', { name: 'Save' });
    await expect(save).toBeDisabled();

    await modal.locator('app-input').first().locator('input').fill('My Discord'); // Name
    await webhook.locator('input').fill('https://discord.com/api/webhooks/123/abc');
    await expect(save).toBeEnabled();
  });

  test('Apprise mode switches between API and CLI fields', async ({ page }) => {
    await loginAndGotoSettings(page, 'notifications');
    const modal = await openProvider(page, 'Apprise');

    const serverUrl = modal.locator('app-input').filter({ hasText: 'Server URL' });
    const serviceUrls = modal.locator('app-chip-input').filter({ hasText: 'Service URLs' });

    // API mode (default) shows Server URL + Config Key; CLI shows Service URLs.
    await expect(serverUrl).toBeVisible();
    await expect(serviceUrls).toHaveCount(0);

    await selectOption(modal, 'Mode', 'CLI');
    await expect(serviceUrls).toBeVisible();
    await expect(serverUrl).toHaveCount(0);
  });

  test('Pushover Emergency priority reveals retry/expire fields', async ({ page }) => {
    await loginAndGotoSettings(page, 'notifications');
    const modal = await openProvider(page, 'Pushover');

    const retry = modal.locator('app-number-input').filter({ hasText: 'Retry' });
    await expect(retry).toHaveCount(0);

    await selectOption(modal, 'Priority', 'Emergency');
    await expect(retry).toBeVisible();
  });
});
