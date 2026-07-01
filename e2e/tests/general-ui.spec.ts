import { test, expect } from '@playwright/test';
import {
  loginAndGotoSettings,
  toggle,
  numberInput,
  expectGuardOnLeave,
  expectNoGuardOnLeave,
} from './helpers/ui';

// Behavior-parity spec for the General settings form (Signal Forms migration).
test.describe('General Settings UI', () => {
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
});
