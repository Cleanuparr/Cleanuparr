import { test, expect } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import { join, resolve } from 'node:path';
import {
  loginAndGetToken,
  createDownloadClient,
  listDownloadClients,
  deleteDownloadClient,
  updateDownloadCleanerConfig,
  getDownloadCleanerConfig,
  updateDeadTorrentConfig,
  triggerJob,
} from './helpers/app-api';
import { QBittorrentDriver } from './helpers/torrent-clients/qbittorrent';
import { buildFolderTorrent, chmodIgnoringEPERM, resetDirectory } from './helpers/torrent-fixtures';

const HOST_DOWNLOADS = resolve(__dirname, '..', 'test-data', 'downloads');
const TARGET = 'cleanuparr-dead';
const SOURCE_CATEGORY = 'dead-src';
const MAX_STRIKES = 3;

async function sleep(ms: number): Promise<void> {
  await new Promise((r) => setTimeout(r, ms));
}

async function waitForTorrent(
  driver: { listTorrents(): Promise<Array<{ hash: string }>> },
  infoHash: string,
  timeoutMs = 20_000,
): Promise<void> {
  const want = infoHash.toLowerCase();
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    const list = await driver.listTorrents();
    if (list.some((t) => t.hash.toLowerCase() === want)) return;
    await sleep(500);
  }
  throw new Error(`Torrent ${infoHash} never appeared`);
}

/**
 * Dead torrent cleanup e2e — a torrent that reports no seeders (its tracker is
 * unreachable: `tracker.invalid`) for `MAX_STRIKES` consecutive Download Cleaner
 * runs is moved to the target category (or tagged), so a seeding rule can act on it.
 */
test.describe.serial('Dead torrent cleanup', () => {
  let token: string;

  test.beforeAll(async () => {
    token = await loginAndGetToken();

    const existing = await listDownloadClients(token);
    for (const client of existing) {
      await deleteDownloadClient(token, client.id);
    }

    const dc = await (await getDownloadCleanerConfig(token)).json();
    await updateDownloadCleanerConfig(token, {
      enabled: true,
      cronExpression: dc.cronExpression || '0 0 * * * ?',
      useAdvancedScheduling: dc.useAdvancedScheduling ?? false,
      ignoredDownloads: [],
    });

    mkdirSync(HOST_DOWNLOADS, { recursive: true });
  });

  test('qBittorrent: moves a dead torrent to the target category', async () => {
    test.setTimeout(180_000);
    const driver = new QBittorrentDriver();
    const slug = 'qbittorrent';
    const scanDir = join(HOST_DOWNLOADS, slug);
    resetDirectory(scanDir);
    chmodIgnoringEPERM(scanDir, 0o777);

    const deadName = `dead-${slug}`;
    const dead = buildFolderTorrent(scanDir, deadName);

    await driver.ready();
    await driver.clearAllTorrents();

    const createRes = await createDownloadClient(token, {
      enabled: true,
      name: 'qBittorrent dead e2e',
      typeName: 'qBittorrent',
      type: 'Torrent',
      host: driver.cleanuparrHost,
      username: driver.username ?? '',
      password: driver.password ?? '',
    });
    expect(createRes.status).toBeGreaterThanOrEqual(200);
    expect(createRes.status).toBeLessThan(300);
    const clientId = (await createRes.json()).id;

    const cfg = await updateDeadTorrentConfig(token, clientId, {
      enabled: true,
      targetCategory: TARGET,
      useTag: false,
      maxStrikes: MAX_STRIKES,
      categories: [SOURCE_CATEGORY],
    });
    expect(cfg.status).toBe(200);

    await driver.addSeedingTorrent({
      metainfo: dead.metainfo,
      savePath: '/downloads',
      category: SOURCE_CATEGORY,
      infoHash: dead.infoHash,
    });
    await waitForTorrent(driver, dead.infoHash);

    // Sanity: the torrent starts in the source category, not the target.
    expect(await driver.getTorrentCategory(dead.infoHash)).toBe(SOURCE_CATEGORY);

    // Each run strikes once; move happens on the run that reaches MAX_STRIKES.
    let moved = false;
    for (let i = 0; i < MAX_STRIKES + 2 && !moved; i++) {
      const trig = await triggerJob(token, 'DownloadCleaner');
      expect(trig.ok, `triggerJob: ${trig.status}`).toBe(true);
      await sleep(13_000); // ride out the job's ~10s Arr-sync delay + processing
      moved = (await driver.getTorrentCategory(dead.infoHash)) === TARGET;
    }

    expect(moved, `dead torrent not moved to ${TARGET} after ${MAX_STRIKES + 2} runs`).toBe(true);
  });
});
