import { test, expect } from '@playwright/test';
import { existsSync, mkdirSync } from 'node:fs';
import { join, resolve } from 'node:path';
import {
  loginAndGetToken,
  createDownloadClient,
  listDownloadClients,
  deleteDownloadClient,
  updateDownloadCleanerConfig,
  getDownloadCleanerConfig,
  updateOrphanedFilesConfig,
  triggerJob,
} from '../helpers/app-api';
import { QBittorrentDriver } from '../helpers/torrent-clients/qbittorrent';
import { buildFolderTorrent, chmodIgnoringEPERM, resetDirectory } from '../helpers/torrent-fixtures';

/**
 * An actively-seeding torrent whose display name
 * differs from its on-disk content path was wrongly moved to the orphaned
 * directory, because the cleaner reconstructed the claimed path as
 * save_path + name instead of using the client-reported content_path.
 *
 * qBittorrent's `torrents/rename` changes the display name only, leaving the
 * data at its original content_path — reproducing the exact divergence. The
 * torrent stays registered (never deleted), so its content must survive the
 * scan.
 *
 * Save path and scan dir are aligned through DownloadDirectorySource/Target
 * so the torrent's content_path resolves into the scanned directory.
 */

const HOST_DOWNLOADS = resolve(__dirname, '..', '..', 'test-data', 'downloads');
// A subdirectory of the qbittorrent bind mount, isolated from the top-level
// dir the client-matrix spec uses.
const SCAN_SUBDIR = 'contentpath-scan';
const HOST_SCAN_DIR = join(HOST_DOWNLOADS, 'qbittorrent', SCAN_SUBDIR);
const HOST_ORPHANED_DIR = join(HOST_SCAN_DIR, 'orphaned');
const CLIENT_SAVE_PATH = `/downloads/${SCAN_SUBDIR}`;
const APP_SCAN_DIR = `/e2e-downloads/qbittorrent/${SCAN_SUBDIR}`;
const APP_ORPHANED_DIR = `${APP_SCAN_DIR}/orphaned`;

async function waitForTorrent(driver: QBittorrentDriver, infoHash: string, timeoutMs = 15_000): Promise<void> {
  const want = infoHash.toLowerCase();
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    const list = await driver.listTorrents();
    if (list.some((t) => t.hash.toLowerCase() === want)) {
      return;
    }
    await new Promise((r) => setTimeout(r, 500));
  }
  throw new Error(`Torrent ${infoHash} not visible after ${timeoutMs}ms`);
}

test.describe.serial('Orphaned files cleanup — content path vs display name', () => {
  const driver = new QBittorrentDriver();
  let token: string;

  test.beforeAll(async () => {
    token = await loginAndGetToken();

    const existing = await listDownloadClients(token);
    for (const client of existing) {
      await deleteDownloadClient(token, client.id);
    }

    const dcCurrent = await (await getDownloadCleanerConfig(token)).json();
    await updateDownloadCleanerConfig(token, {
      enabled: true,
      cronExpression: dcCurrent.cronExpression || '0 0 * * * ?',
      useAdvancedScheduling: dcCurrent.useAdvancedScheduling ?? false,
      ignoredDownloads: [],
    });

    mkdirSync(HOST_DOWNLOADS, { recursive: true });
    await driver.ready();
    await driver.clearAllTorrents();
  });

  test('renamed torrent keeps its data out of the orphaned directory', async () => {
    test.setTimeout(180_000);

    resetDirectory(HOST_SCAN_DIR);
    mkdirSync(HOST_ORPHANED_DIR, { recursive: true });
    chmodIgnoringEPERM(HOST_ORPHANED_DIR, 0o777);

    const diskName = 'keep-renamed';
    const fx = buildFolderTorrent(HOST_SCAN_DIR, diskName);

    const createRes = await createDownloadClient(token, {
      enabled: true,
      name: 'qBittorrent content-path',
      typeName: driver.typeName,
      type: 'Torrent',
      host: driver.cleanuparrHost,
      username: driver.username ?? '',
      password: driver.password ?? '',
      downloadDirectorySource: '/downloads',
      downloadDirectoryTarget: '/e2e-downloads/qbittorrent',
    });
    expect(createRes.ok, `createDownloadClient: ${createRes.status}`).toBe(true);
    const clientId = (await createRes.json()).id;

    const ofcRes = await updateOrphanedFilesConfig(token, clientId, {
      enabled: true,
      scanDirectories: [APP_SCAN_DIR],
      orphanedDirectory: APP_ORPHANED_DIR,
      minFileAgeHours: 0,
    });
    expect(ofcRes.status).toBe(200);

    await driver.addTorrent({
      metainfo: fx.metainfo,
      savePath: CLIENT_SAVE_PATH,
      name: diskName,
      infoHash: fx.infoHash,
    });
    await waitForTorrent(driver, fx.infoHash);

    // Diverge the display name from the on-disk folder.
    await driver.renameTorrent(fx.infoHash, 'keep-renamed-DISPLAY');

    // Rename must not have moved files on disk (guards the repro's premise).
    expect(existsSync(join(HOST_SCAN_DIR, diskName, 'data.bin'))).toBe(true);
    const afterRename = await driver.listTorrents();
    expect(afterRename.some((t) => t.hash.toLowerCase() === fx.infoHash.toLowerCase())).toBe(true);

    const trig = await triggerJob(token, 'DownloadCleaner');
    expect(trig.ok, `triggerJob: ${trig.status}`).toBe(true);
    // The move is async and lands ~6s after the trigger. Asserting a negative
    // (data stays put) must wait well past that window, else a buggy build that
    // does move the data races the assertion and false-passes.
    await new Promise((r) => setTimeout(r, 20_000));

    // The fix: content_path keeps the data claimed, so it stays put.
    expect(existsSync(join(HOST_SCAN_DIR, diskName, 'data.bin'))).toBe(true);
    expect(existsSync(join(HOST_ORPHANED_DIR, diskName))).toBe(false);
    expect(existsSync(join(HOST_ORPHANED_DIR, 'keep-renamed-DISPLAY'))).toBe(false);
  });
});
