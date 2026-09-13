import { test, expect, type Page } from '@playwright/test';
import {
  loginAndGotoSettings,
  toggle,
  numberInput,
  addChip,
  isToggleOn,
  ensureToggle,
  saveSettings,
  expectGuardOnLeave,
  expectNoGuardOnLeave,
} from '../helpers/ui';

// Behavior-parity spec for the General settings form (Signal Forms migration).
test.describe('General Settings UI', () => {
  const ignoredChip = (page: Page, value: string) =>
    page.locator('app-chip-input').filter({ hasText: 'Ignored Downloads' }).locator('.chip').filter({ hasText: value });


  test('local-auth-bypass toggle reveals and hides nested fields', async ({ page }) => {
    await loginAndGotoSettings(page, 'general');

    // Nested fields hidden until the master toggle is on.
    await expect(toggle(page, 'Trust Forwarded Headers')).toHaveCount(0);
    await expect(page.locator('app-chip-input').filter({ hasText: 'Additional Trusted Networks' })).toHaveCount(0);

    await toggle(page, 'Disable Authentication for Local Addresses').click();
    await expect(toggle(page, 'Trust Forwarded Headers')).toBeVisible();
    await expect(page.locator('app-chip-input').filter({ hasText: 'Additional Trusted Networks' })).toBeVisible();

    await toggle(page, 'Disable Authentication for Local Addresses').click();
    await expect(toggle(page, 'Trust Forwarded Headers')).toHaveCount(0);
  });

  test('strike inactivity window enforces its max bound', async ({ page }) => {
    await loginAndGotoSettings(page, 'general');

    const fieldEl = page.locator('app-number-input').filter({ hasText: 'Strike Inactivity Window' });
    await numberInput(page, 'Strike Inactivity Window').fill('200');
    await expect(fieldEl.getByText(/168/)).toBeVisible();

    await numberInput(page, 'Strike Inactivity Window').fill('24');
    await expect(fieldEl.getByText(/168/)).toHaveCount(0);
  });

  test('unsaved-changes guard fires when dirty and clears after revert', async ({ page }) => {
    await loginAndGotoSettings(page, 'general');

    await toggle(page, 'Dry Run Mode').click();
    await expectGuardOnLeave(page);

    await toggle(page, 'Dry Run Mode').click();
    await expectNoGuardOnLeave(page);
  });

  test('saved general settings survive a reload', async ({ page }) => {
    const ignored = `ui-ignored-${Date.now()}`;
    await loginAndGotoSettings(page, 'general');

    const bannerBefore = await isToggleOn(toggle(page, 'Display Support Banner'));
    const windowBefore = await numberInput(page, 'Strike Inactivity Window').inputValue();
    // The field is bounded 1..168, so pick a value that always differs from the saved one.
    const windowAfter = windowBefore === '42' ? '24' : '42';

    await toggle(page, 'Display Support Banner').click();
    await numberInput(page, 'Strike Inactivity Window').fill(windowAfter);
    await addChip(page, 'Ignored Downloads', ignored);
    await saveSettings(page, 'general');

    // The save above is the first persisted change, so everything past it restores in `finally`.
    // The app container is only restarted between spec folders, so a leak here reaches the rest of them.
    try {
      await page.reload();
      await expect(toggle(page, 'Display Support Banner')).toHaveAttribute('aria-checked', String(!bannerBefore));
      await expect(numberInput(page, 'Strike Inactivity Window')).toHaveValue(windowAfter);
      await expect(ignoredChip(page, ignored)).toBeVisible();
    } finally {
      await page.reload();
      await ensureToggle(toggle(page, 'Display Support Banner'), bannerBefore);
      await numberInput(page, 'Strike Inactivity Window').fill(windowBefore);
      // The chip is gone when it failed to persist, and clicking a missing Remove would
      // time out and replace the assertion error that got us here.
      if (await ignoredChip(page, ignored).count()) {
        await ignoredChip(page, ignored).getByRole('button', { name: 'Remove' }).click();
      }
      await saveSettings(page, 'general');
    }
  });
});
