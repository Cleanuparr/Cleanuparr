import { test, expect, type Page } from '@playwright/test';
import {
  loginAndGotoSettings,
  toggle,
  selectOption,
  ensureToggle,
  ensureAccordionExpanded,
  expectGuardOnLeave,
  expectNoGuardOnLeave,
} from '../helpers/ui';

// Behavior-parity spec for the Queue Cleaner settings form + stall-rule modal.
test.describe('Queue Cleaner UI', () => {
  async function openStallModal(page: Page) {
    await ensureToggle(toggle(page, 'Enabled'), true);
    await ensureAccordionExpanded(
      page,
      'Stalled Download Rules',
      page.getByRole('button', { name: 'Add Stall Rule' }),
    );
    await page.getByRole('button', { name: 'Add Stall Rule' }).click();
    const modal = page.getByRole('dialog', { name: 'Add Stall Rule' });
    await expect(modal).toBeVisible();
    return modal;
  }

  test('master Enabled gates the form; advanced scheduling swaps fields', async ({ page }) => {
    await loginAndGotoSettings(page, 'queue-cleaner');
    const enabled = toggle(page, 'Enabled');

    await ensureToggle(enabled, false);
    await expect(toggle(page, 'Advanced Scheduling')).toHaveCount(0);

    await ensureToggle(enabled, true);
    const adv = toggle(page, 'Advanced Scheduling');
    await expect(adv).toBeVisible();
    await expect(page.locator('app-select').filter({ hasText: 'Schedule Unit' })).toBeVisible();

    await ensureToggle(adv, true);
    await expect(page.locator('app-input').filter({ hasText: 'Cron Expression' })).toBeVisible();
    await expect(page.locator('app-select').filter({ hasText: 'Schedule Unit' })).toHaveCount(0);
  });

  test('stall modal shows name-required and completion-range validation', async ({ page }) => {
    await loginAndGotoSettings(page, 'queue-cleaner');
    const modal = await openStallModal(page);

    const nameCard = modal.locator('app-input').filter({ hasText: 'Name' });
    await expect(nameCard.locator('.input-error')).toBeVisible();
    await nameCard.locator('input').fill('My Rule');
    await expect(nameCard.locator('.input-error')).toHaveCount(0);

    const maxCard = modal.locator('app-number-input').filter({ hasText: 'Max Completion' });
    await modal.locator('app-number-input').filter({ hasText: 'Min Completion' }).locator('input').fill('80');
    await maxCard.locator('input').fill('10');
    await expect(maxCard.locator('.number-error')).toBeVisible();

    await modal.getByRole('button', { name: 'Cancel' }).click();
    await expect(modal).toBeHidden();
  });

  test('stall modal: reset-on-progress reveals size field; public disables delete-private', async ({ page }) => {
    await loginAndGotoSettings(page, 'queue-cleaner');
    const modal = await openStallModal(page);

    const minProgress = modal.locator('app-size-input').filter({ hasText: 'Minimum Progress' });
    await expect(minProgress).toHaveCount(0);
    await modal.getByRole('switch', { name: 'Reset Strikes on Progress' }).click();
    await expect(minProgress).toBeVisible();

    await selectOption(modal, 'Privacy Type', 'Public');
    await expect(modal.getByRole('switch', { name: 'Delete Private from Client' })).toBeDisabled();

    await modal.getByRole('button', { name: 'Cancel' }).click();
  });

  async function openSlowModal(page: Page) {
    await ensureToggle(toggle(page, 'Enabled'), true);
    await ensureAccordionExpanded(
      page,
      'Slow Download Rules',
      page.getByRole('button', { name: 'Add Slow Rule' }),
    );
    await page.getByRole('button', { name: 'Add Slow Rule' }).click();
    const modal = page.getByRole('dialog', { name: 'Add Slow Rule' });
    await expect(modal).toBeVisible();
    return modal;
  }

  test('slow modal validates name + completion range and disables delete-private for public', async ({ page }) => {
    await loginAndGotoSettings(page, 'queue-cleaner');
    const modal = await openSlowModal(page);

    const nameCard = modal.locator('app-input').filter({ hasText: 'Name' });
    await expect(nameCard.locator('.input-error')).toBeVisible();
    await nameCard.locator('input').fill('My Slow Rule');
    await expect(nameCard.locator('.input-error')).toHaveCount(0);

    const maxCard = modal.locator('app-number-input').filter({ hasText: 'Max Completion' });
    await modal.locator('app-number-input').filter({ hasText: 'Min Completion' }).locator('input').fill('80');
    await maxCard.locator('input').fill('10');
    await expect(maxCard.locator('.number-error')).toBeVisible();

    await selectOption(modal, 'Privacy Type', 'Public');
    await expect(modal.getByRole('switch', { name: 'Delete Private from Client' })).toBeDisabled();

    await modal.getByRole('button', { name: 'Cancel' }).click();
    await expect(modal).toBeHidden();
  });

  test('unsaved-changes guard fires when dirty and clears after revert', async ({ page }) => {
    await loginAndGotoSettings(page, 'queue-cleaner');
    const enabled = toggle(page, 'Enabled');

    await enabled.click();
    await expectGuardOnLeave(page);
    await enabled.click();
    await expectNoGuardOnLeave(page);
  });
});
