import { test, expect } from '@playwright/test';
import { existsSync, mkdirSync, readdirSync, writeFileSync } from 'node:fs';
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
} from './helpers/app-api';
import { QBittorrentDriver } from './helpers/torrent-clients/qbittorrent';
import { chmodIgnoringEPERM, resetDirectory } from './helpers/torrent-fixtures';

/**
 * Regression guard for the orphaned-files cleanup safety bail.
 *
 * The cleaner refuses to move anything for a download client when it cannot
 * trust the client's torrent list — either because the call threw (client
 * unreachable / authentication broken) or because the client reported 0
 * torrents. Without this guard, an empty/erroring client makes every file in
 * the scan directory look orphaned and real downloads get moved.
 *
 * Two scenarios, both assert "files remain in the scan dir":
 *
 *   1. Unreachable host — client registered against a port nothing listens
 *      on. Exercises the catch path (or upstream LoginAsync skip — both lead
 *      to the same user-visible outcome).
 *   2. Reachable client with 0 torrents — qBittorrent up but empty.
 *      Exercises the explicit zero-torrents bail in `TryAddClaimedPathsAsync`.
 */

const HOST_DOWNLOADS = resolve(__dirname, '..', 'test-data', 'downloads');
const APP_DOWNLOADS = '/e2e-downloads';
const SLUG = 'qbittorrent-unreachable';
const HOST_SCAN_DIR = join(HOST_DOWNLOADS, SLUG);
const HOST_ORPHANED_DIR = join(HOST_DOWNLOADS, SLUG, 'orphaned');
const APP_SCAN_DIR = `${APP_DOWNLOADS}/${SLUG}`;
const APP_ORPHANED_DIR = `${APP_DOWNLOADS}/${SLUG}/orphaned`;

function writeFile(dir: string, name: string, content = 'real-download'): string {
  mkdirSync(dir, { recursive: true });
  chmodIgnoringEPERM(dir, 0o777);
  const path = join(dir, name);
  writeFileSync(path, content);
  return path;
}

async function triggerAndSettle(token: string): Promise<void> {
  const res = await triggerJob(token, 'DownloadCleaner');
  expect(res.ok, `triggerJob: ${res.status}`).toBe(true);
  // No seeding downloads to wait on -> only the cleaner's own bookkeeping +
  // filesystem walk. 3s is the same window used by orphaned-files-behaviors.
  await new Promise((r) => setTimeout(r, 3000));
}

test.describe.serial('Orphaned files cleanup — refuses to scan when client data is untrusted', () => {
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
  });

  test.beforeEach(async () => {
    // Each scenario starts with a fresh scan dir and a single fake real
    // download. If the scanner runs incorrectly the file gets moved into
    // HOST_ORPHANED_DIR — that's the regression we're guarding against.
    resetDirectory(HOST_SCAN_DIR);
    mkdirSync(HOST_ORPHANED_DIR, { recursive: true });
    chmodIgnoringEPERM(HOST_ORPHANED_DIR, 0o777);

    // Each test registers its own client; remove anything stale.
    const existing = await listDownloadClients(token);
    for (const client of existing) {
      await deleteDownloadClient(token, client.id);
    }
  });

  test('Unreachable download client → files in the scan dir are not moved', async () => {
    test.setTimeout(60_000);

    const realDownload = writeFile(HOST_SCAN_DIR, 'real-download.mkv');

    const createRes = await createDownloadClient(token, {
      enabled: true,
      name: 'qBittorrent unreachable',
      typeName: 'qBittorrent',
      type: 'Torrent',
      // Port 1 — nothing listens here. Cleanuparr's qBit client will fail to
      // connect when the cleaner runs.
      host: 'http://127.0.0.1:1',
      username: 'admin',
      password: 'adminadmin',
      downloadDirectorySource: '/downloads',
      downloadDirectoryTarget: APP_SCAN_DIR,
    });
    expect(createRes.ok, `createDownloadClient: ${createRes.status}`).toBe(true);
    const created = await createRes.json();

    const ofcRes = await updateOrphanedFilesConfig(token, created.id, {
      enabled: true,
      scanDirectories: [APP_SCAN_DIR],
      orphanedDirectory: APP_ORPHANED_DIR,
      minFileAgeHours: 0,
    });
    expect(ofcRes.ok, `updateOrphanedFilesConfig: ${ofcRes.status}`).toBe(true);

    await triggerAndSettle(token);

    expect(existsSync(realDownload)).toBe(true);
    expect(existsSync(join(HOST_ORPHANED_DIR, 'real-download.mkv'))).toBe(false);
    expect(readdirSync(HOST_ORPHANED_DIR).length).toBe(0);
  });

  test('Reachable client reporting 0 torrents → files in the scan dir are not moved', async () => {
    test.setTimeout(120_000);

    const driver = new QBittorrentDriver();
    await driver.ready();
    await driver.clearAllTorrents();

    const realDownload = writeFile(HOST_SCAN_DIR, 'real-download.mkv');

    const createRes = await createDownloadClient(token, {
      enabled: true,
      name: 'qBittorrent empty',
      typeName: driver.typeName,
      type: 'Torrent',
      host: driver.cleanuparrHost,
      username: driver.username ?? '',
      password: driver.password ?? '',
      downloadDirectorySource: '/downloads',
      downloadDirectoryTarget: APP_SCAN_DIR,
    });
    expect(createRes.ok, `createDownloadClient: ${createRes.status}`).toBe(true);
    const created = await createRes.json();

    const ofcRes = await updateOrphanedFilesConfig(token, created.id, {
      enabled: true,
      scanDirectories: [APP_SCAN_DIR],
      orphanedDirectory: APP_ORPHANED_DIR,
      minFileAgeHours: 0,
    });
    expect(ofcRes.ok, `updateOrphanedFilesConfig: ${ofcRes.status}`).toBe(true);

    await triggerAndSettle(token);

    expect(existsSync(realDownload)).toBe(true);
    expect(existsSync(join(HOST_ORPHANED_DIR, 'real-download.mkv'))).toBe(false);
    expect(readdirSync(HOST_ORPHANED_DIR).length).toBe(0);
  });
});
