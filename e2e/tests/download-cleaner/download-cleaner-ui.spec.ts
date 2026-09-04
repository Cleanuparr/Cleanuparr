import { test, expect, type Page } from '@playwright/test';
import {
  loginAndGetToken,
  createDownloadClient,
  deleteDownloadClient,
  listDownloadClients,
} from '../helpers/app-api';
import {
  loginAndGotoSettings,
  toggle,
  select,
  selectOption,
  ensureToggle,
  ensureAccordionExpanded,
  textInput,
  numberInput,
  addChip,
} from '../helpers/ui';

const CLIENT_A = 'E2E DC One';
const CLIENT_B = 'E2E DC Two';
const CLIENT_TRANSMISSION = 'E2E DC Transmission';

function clientPayload(name: string) {
  return {
    enabled: true,
    name,
    type: 'Torrent',
    typeName: 'qBittorrent',
    host: 'http://localhost:8090',
    username: 'admin',
    password: 'adminadmin',
    urlBase: '',
    downloadDirectorySource: null,
    downloadDirectoryTarget: null,
  };
}

function transmissionClientPayload(name: string) {
  return {
    ...clientPayload(name),
    typeName: 'Transmission',
    host: 'http://localhost:9091',
    username: 'transmission',
    password: 'transmission',
  };
}

// Behavior-parity spec for the Download Cleaner form: global config, seeding-rule modal,
// and the per-client sub-configs (the linkedSignal-backed part with the highest migration risk).
test.describe.serial('Download Cleaner UI', () => {
  const createdIds: string[] = [];

  test.beforeAll(async () => {
    const token = await loginAndGetToken();
    await createDownloadClient(token, clientPayload(CLIENT_A));
    await createDownloadClient(token, clientPayload(CLIENT_B));
    await createDownloadClient(token, transmissionClientPayload(CLIENT_TRANSMISSION));
    const clients = await listDownloadClients(token);
    for (const c of clients) {
      if (c.name === CLIENT_A || c.name === CLIENT_B || c.name === CLIENT_TRANSMISSION) createdIds.push(c.id);
    }
  });

  test.afterAll(async () => {
    const token = await loginAndGetToken();
    for (const id of createdIds) {
      await deleteDownloadClient(token, id).catch(() => undefined);
    }
  });

  async function enableAndSelect(page: Page, clientName = CLIENT_A) {
    await loginAndGotoSettings(page, 'download-cleaner');
    await ensureToggle(toggle(page, 'Enabled'), true);
    await expect(select(page, 'Download Client')).toBeVisible();
    await selectOption(page, 'Download Client', clientName);
  }

  test('global Enabled gates the schedule and the per-client card', async ({ page }) => {
    await loginAndGotoSettings(page, 'download-cleaner');
    const enabled = toggle(page, 'Enabled');

    await ensureToggle(enabled, false);
    await expect(toggle(page, 'Advanced Scheduling')).toHaveCount(0);
    await expect(select(page, 'Download Client')).toHaveCount(0);

    await ensureToggle(enabled, true);
    await expect(toggle(page, 'Advanced Scheduling')).toBeVisible();
    await expect(select(page, 'Download Client')).toBeVisible();
  });

  test('seeding-rule modal requires a name and at least one category', async ({ page }) => {
    await enableAndSelect(page);
    await ensureAccordionExpanded(page, 'Seeding Rules', page.getByRole('button', { name: 'Add Seeding Rule' }));
    await page.getByRole('button', { name: 'Add Seeding Rule' }).click();

    const modal = page.getByRole('dialog', { name: 'Add Seeding Rule' });
    await expect(modal).toBeVisible();

    const nameCard = modal.locator('app-input').filter({ hasText: 'Rule Name' });
    await expect(nameCard.locator('.input-error')).toBeVisible();

    const catCard = modal.locator('app-chip-input').filter({ hasText: 'Categories' });
    await expect(catCard.locator('.chip-error')).toBeVisible();

    await nameCard.locator('input').fill('My Rule');
    await expect(nameCard.locator('.input-error')).toHaveCount(0);
  });

  test('saves Max Inactive Days and shows it on the rule card', async ({ page }) => {
    await enableAndSelect(page);
    await ensureAccordionExpanded(page, 'Seeding Rules', page.getByRole('button', { name: 'Add Seeding Rule' }));
    await page.getByRole('button', { name: 'Add Seeding Rule' }).click();

    const modal = page.getByRole('dialog', { name: 'Add Seeding Rule' });
    await expect(modal).toBeVisible();

    await textInput(modal, 'Rule Name').fill('Inactive Rule');
    await addChip(modal, 'Categories', 'e2e-inactive');
    await numberInput(modal, 'Max Ratio').fill('2');
    await numberInput(modal, 'Max Inactive Days').fill('30');

    await modal.getByRole('button', { name: 'Create', exact: true }).click();
    await expect(modal).toBeHidden({ timeout: 10_000 });

    const card = page.locator('.rule-card').filter({ hasText: 'Inactive Rule' });
    await expect(card.getByText('Max Inactive: 30d', { exact: true })).toBeVisible({ timeout: 10_000 });
  });

  test('Max Inactive Days is hidden for a client that does not support it', async ({ page }) => {
    await enableAndSelect(page, CLIENT_TRANSMISSION);
    await ensureAccordionExpanded(page, 'Seeding Rules', page.getByRole('button', { name: 'Add Seeding Rule' }));
    await page.getByRole('button', { name: 'Add Seeding Rule' }).click();

    const modal = page.getByRole('dialog', { name: 'Add Seeding Rule' });
    await expect(modal).toBeVisible();
    await expect(modal.locator('app-number-input').filter({ hasText: 'Min Seeders' })).toBeVisible();
    await expect(modal.locator('app-number-input').filter({ hasText: 'Max Inactive Days' })).toHaveCount(0);
  });

  test('Delete Source Files is shown only for the delete action', async ({ page }) => {
    await enableAndSelect(page);
    await ensureAccordionExpanded(page, 'Seeding Rules', page.getByRole('button', { name: 'Add Seeding Rule' }));
    await page.getByRole('button', { name: 'Add Seeding Rule' }).click();

    const modal = page.getByRole('dialog', { name: 'Add Seeding Rule' });
    await expect(modal).toBeVisible();
    await expect(toggle(modal, 'Delete Source Files')).toBeVisible();

    await selectOption(modal, 'Action', 'Stop');
    await expect(toggle(modal, 'Delete Source Files')).toHaveCount(0);

    await selectOption(modal, 'Action', 'Delete');
    await expect(toggle(modal, 'Delete Source Files')).toBeVisible();
  });

  test('saves the stop action and shows it on the rule card', async ({ page }) => {
    await enableAndSelect(page);
    await ensureAccordionExpanded(page, 'Seeding Rules', page.getByRole('button', { name: 'Add Seeding Rule' }));
    await page.getByRole('button', { name: 'Add Seeding Rule' }).click();

    const modal = page.getByRole('dialog', { name: 'Add Seeding Rule' });
    await expect(modal).toBeVisible();

    await textInput(modal, 'Rule Name').fill('Stop Rule');
    await addChip(modal, 'Categories', 'e2e-stop');
    await numberInput(modal, 'Max Ratio').fill('2');
    await selectOption(modal, 'Action', 'Stop');

    await modal.getByRole('button', { name: 'Create', exact: true }).click();
    await expect(modal).toBeHidden({ timeout: 10_000 });

    const card = page.locator('.rule-card').filter({ hasText: 'Stop Rule' });
    await expect(card.getByText('Action: Stop', { exact: true })).toBeVisible({ timeout: 10_000 });
    await expect(card.getByText(/^Delete Files:/)).toHaveCount(0);
  });

  test('per-client Dead Torrents requires strikes >= 3', async ({ page }) => {
    await enableAndSelect(page);
    const accordion = page.locator('app-accordion').filter({ hasText: 'Dead Torrents' });
    await ensureAccordionExpanded(page, 'Dead Torrents', accordion.getByRole('switch', { name: 'Enabled' }));

    await accordion.getByRole('switch', { name: 'Enabled' }).click();
    const strikes = accordion.locator('app-number-input').filter({ hasText: 'Strikes' });
    await strikes.locator('input').fill('1');
    await expect(strikes.locator('.number-error')).toBeVisible();

    await strikes.locator('input').fill('5');
    await expect(strikes.locator('.number-error')).toHaveCount(0);
  });

  test('switching client with unsaved per-client edits prompts to discard', async ({ page }) => {
    await enableAndSelect(page, CLIENT_A);

    // Make a per-client edit (enable Dead Torrents) so the client's sub-config is dirty.
    const accordion = page.locator('app-accordion').filter({ hasText: 'Dead Torrents' });
    await ensureAccordionExpanded(page, 'Dead Torrents', accordion.getByRole('switch', { name: 'Enabled' }));
    await accordion.getByRole('switch', { name: 'Enabled' }).click();

    // Switching clients must prompt to discard the unsaved edits.
    await selectOption(page, 'Download Client', CLIENT_B);
    const dialog = page.getByRole('alertdialog', { name: 'Unsaved Changes' });
    await expect(dialog).toBeVisible({ timeout: 5_000 });
    await dialog.getByRole('button', { name: 'Cancel', exact: true }).click();
    await expect(dialog).toBeHidden();
  });
});
