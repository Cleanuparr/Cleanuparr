import { test, expect } from '@playwright/test';
import { loginAndGotoSettings, textInput } from '../helpers/ui';
import { TEST_CONFIG } from '../helpers/test-config';

// Behavior-parity spec for the non-OIDC Account settings sections.
// (OIDC is covered separately in oidc-settings-ui.spec.ts.)
// Establishes coverage before the account-settings component is refactored.
test.describe('Account Settings UI', () => {
  test('renders the account section cards', async ({ page }) => {
    await loginAndGotoSettings(page, 'account');
    for (const title of ['Change Username', 'Change Password', 'Two-Factor Authentication', 'API Key', 'Plex Integration']) {
      await expect(page.locator('app-card').filter({ hasText: title })).toBeVisible();
    }
  });

  test('username change prefills the current name and is gated on a password', async ({ page }) => {
    await loginAndGotoSettings(page, 'account');
    const card = page.locator('app-card').filter({ hasText: 'Change Username' });

    await expect(textInput(card, 'New Username')).toHaveValue(TEST_CONFIG.adminUsername);

    const submit = card.getByRole('button', { name: 'Change Username' });
    await expect(submit).toBeDisabled();

    await textInput(card, 'Current Password').fill(TEST_CONFIG.adminPassword);
    await expect(submit).toBeEnabled();
  });

  test('API key reveal toggles between masked and revealed', async ({ page }) => {
    await loginAndGotoSettings(page, 'account');
    const card = page.locator('app-card').filter({ hasText: 'API Key' });

    await expect(card.getByRole('button', { name: 'Reveal' })).toBeVisible();
    await expect(card.getByRole('button', { name: 'Copy' })).toHaveCount(0);

    await card.getByRole('button', { name: 'Reveal' }).click();
    await expect(card.getByRole('button', { name: 'Hide' })).toBeVisible();
    await expect(card.getByRole('button', { name: 'Copy' })).toBeVisible();

    await card.getByRole('button', { name: 'Hide' }).click();
    await expect(card.getByRole('button', { name: 'Reveal' })).toBeVisible();
  });

  test('2FA enable button is gated on a password', async ({ page }) => {
    await loginAndGotoSettings(page, 'account');
    const card = page.locator('app-card').filter({ hasText: 'Two-Factor Authentication' });

    await expect(card.getByText('Disabled', { exact: true })).toBeVisible();
    const enableBtn = card.getByRole('button', { name: 'Enable 2FA' });
    await expect(enableBtn).toBeDisabled();

    await textInput(card, 'Password').fill('a-password');
    await expect(enableBtn).toBeEnabled();
  });

  test('Plex integration offers linking when not linked', async ({ page }) => {
    await loginAndGotoSettings(page, 'account');
    const card = page.locator('app-card').filter({ hasText: 'Plex Integration' });
    await expect(card.getByRole('button', { name: 'Link Plex Account' })).toBeVisible();
  });
});
