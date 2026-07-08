import { test, expect } from '@playwright/test';
import {
  loginAndGotoSettings,
  toggle,
  ensureToggle,
  expectGuardOnLeave,
  expectNoGuardOnLeave,
} from '../helpers/ui';

// Behavior-parity spec for the Seeker settings form.
test.describe('Seeker UI', () => {
  const interval = (page: import('@playwright/test').Page) =>
    page.locator('app-select').filter({ hasText: 'Search Interval' });
  const proactiveCard = (page: import('@playwright/test').Page) =>
    page.locator('app-card').filter({ hasText: 'Proactive Search' });
  const strategy = (page: import('@playwright/test').Page) =>
    page.locator('app-select').filter({ hasText: 'Selection Strategy' });

  test('Search Enabled gates the interval and the Proactive Search card', async ({ page }) => {
    await loginAndGotoSettings(page, 'seeker');
    const searchEnabled = toggle(page, 'Search Enabled');

    await ensureToggle(searchEnabled, false);
    await expect(interval(page)).toHaveCount(0);
    await expect(toggle(page, 'Proactive Search')).toHaveCount(0);

    await ensureToggle(searchEnabled, true);
    await expect(interval(page)).toBeVisible();
    await expect(toggle(page, 'Proactive Search')).toBeVisible();
  });

  test('Proactive Search gates the Selection Strategy select', async ({ page }) => {
    await loginAndGotoSettings(page, 'seeker');
    await ensureToggle(toggle(page, 'Search Enabled'), true);
    const proactive = toggle(page, 'Proactive Search');

    await ensureToggle(proactive, false);
    await expect(strategy(page)).toHaveCount(0);

    await ensureToggle(proactive, true);
    await expect(strategy(page)).toBeVisible();
  });

  test('Round Robin toggle asks for confirmation', async ({ page }) => {
    await loginAndGotoSettings(page, 'seeker');
    await ensureToggle(toggle(page, 'Search Enabled'), true);
    await ensureToggle(toggle(page, 'Proactive Search'), true);

    await toggle(page, 'Round Robin').click();
    const dialog = page.getByRole('alertdialog');
    await expect(dialog).toBeVisible({ timeout: 5_000 });
    // Dismiss without applying the change.
    await dialog.getByRole('button').first().click();
    await expect(dialog).toBeHidden();
  });

  test('unsaved-changes guard fires when dirty and clears after revert', async ({ page }) => {
    await loginAndGotoSettings(page, 'seeker');
    const searchEnabled = toggle(page, 'Search Enabled');

    await searchEnabled.click();
    await expectGuardOnLeave(page);
    await searchEnabled.click();
    await expectNoGuardOnLeave(page);
  });
});
