import { test, expect } from '@playwright/test';
import {
  loginAndGotoSettings,
  toggle,
  textInput,
  expectGuardOnLeave,
  expectNoGuardOnLeave,
} from '../helpers/ui';

// Behavior-parity spec for the Blacklist Sync settings form (Signal Forms migration).
// Authored against pre-migration (main) behavior; must stay green after the migration.
test.describe('Blacklist Sync UI', () => {
  const pathField = (page: import('@playwright/test').Page) =>
    page.locator('app-input').filter({ hasText: 'Blacklist File Path' });

  test('path field is gated by Enabled and required when enabled', async ({ page }) => {
    await loginAndGotoSettings(page, 'blacklist-sync');

    // Hidden while disabled.
    await expect(pathField(page)).toHaveCount(0);

    // Enabling reveals the path field.
    await toggle(page, 'Enabled').click();
    await expect(pathField(page)).toBeVisible();

    // Empty path -> required error + Save disabled.
    const save = page.getByRole('button', { name: /Save Settings|Saved!/ });
    await expect(page.getByText(/required when blacklist sync is enabled/i)).toBeVisible();
    await expect(save).toBeDisabled();

    // Filling the path clears the error and enables Save.
    await textInput(page, 'Blacklist File Path').fill('/config/blacklist.txt');
    await expect(page.getByText(/required when blacklist sync is enabled/i)).toHaveCount(0);
    await expect(save).toBeEnabled();
  });

  test('unsaved-changes guard fires when dirty and clears after revert', async ({ page }) => {
    await loginAndGotoSettings(page, 'blacklist-sync');

    // Toggling Enabled makes the form dirty -> guard on leave.
    await toggle(page, 'Enabled').click();
    await expectGuardOnLeave(page);

    // Reverting the toggle restores the saved snapshot -> no guard on leave.
    await toggle(page, 'Enabled').click();
    await expectNoGuardOnLeave(page);
  });
});
