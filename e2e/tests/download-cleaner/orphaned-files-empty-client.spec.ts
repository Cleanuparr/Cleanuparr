import { test, expect } from '@playwright/test';
import { existsSync, readdirSync, statSync, utimesSync } from 'node:fs';
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
  OrphanedFilesConfigRequest,
} from '../helpers/app-api';
import { QBittorrentDriver } from '../helpers/torrent-clients/qbittorrent';
import { resetDirectory } from '../helpers/torrent-fixtures';
import { mkdirShared, writeFileShared } from '../helpers/shared-volume';

/**
 * Regression guard for issue #746.
 *
 * A download client holding no torrents claims no paths, so every entry in
 * its scan directories is orphaned. The cleaner used to read an empty torrent
 * list as "do not trust this client" and skip the scan and the purge, which
 * stranded orphans on disk until the user added a torrent.
 *
 * A client whose list call throws is the opposite case and still bails. See
 * `orphaned-files-unreachable-client.spec.ts`.
 *
 * `utimesSync` backdates mtime for the purge path, which reads only
 * `GetLastWriteTimeUtc`. It cannot fake the move path's MinFileAgeHours
 * check, which takes `MAX(lastWrite, created)`, and Linux birthtime resists
 * portable backdating. Unit tests cover that combination.
 */

const HOST_DOWNLOADS = resolve(__dirname, '..', '..', 'test-data', 'downloads');
const APP_DOWNLOADS = '/e2e-downloads';
const SLUG = 'qbittorrent-empty';
const HOST_SCAN_DIR = join(HOST_DOWNLOADS, SLUG);
const HOST_ORPHANED_DIR = join(HOST_DOWNLOADS, SLUG, 'orphaned');
const APP_SCAN_DIR = `${APP_DOWNLOADS}/${SLUG}`;
const APP_ORPHANED_DIR = `${APP_DOWNLOADS}/${SLUG}/orphaned`;

// The unreachable sibling in the last test needs its own directory pair.
// Validation rejects a scan directory that overlaps another client's.
const SIBLING_SLUG = 'qbittorrent-noconn';
const HOST_SIBLING_SCAN_DIR = join(HOST_DOWNLOADS, SIBLING_SLUG);
const HOST_SIBLING_ORPHANED_DIR = join(HOST_DOWNLOADS, SIBLING_SLUG, 'orphaned');
const APP_SIBLING_SCAN_DIR = `${APP_DOWNLOADS}/${SIBLING_SLUG}`;
const APP_SIBLING_ORPHANED_DIR = `${APP_DOWNLOADS}/${SIBLING_SLUG}/orphaned`;

function backdateRecursive(path: string, hoursAgo: number): void {
  const t = (Date.now() - hoursAgo * 3600_000) / 1000;
  const visit = (p: string) => {
    utimesSync(p, t, t);
    if (statSync(p).isDirectory()) {
      for (const e of readdirSync(p)) visit(join(p, e));
    }
  };
  visit(path);
}

function writeOrphanFile(dir: string, name: string, content = 'orphan'): string {
  mkdirShared(dir);
  const path = join(dir, name);
  writeFileShared(path, content);
  return path;
}

async function waitForCondition(
  predicate: () => boolean | Promise<boolean>,
  timeoutMs: number,
  label: string,
): Promise<void> {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    if (await predicate()) {
      return;
    }
    await new Promise((r) => setTimeout(r, 500));
  }
  throw new Error(`Timed out after ${timeoutMs}ms waiting for: ${label}`);
}

async function triggerAndSettle(token: string): Promise<void> {
  const res = await triggerJob(token, 'DownloadCleaner');
  expect(res.ok, `triggerJob: ${res.status}`).toBe(true);
  // The cleaner walks the directories on a worker thread.
  // Positive assertions poll after this window.
  await new Promise((r) => setTimeout(r, 3000));
}

test.describe.serial('Orphaned files cleanup — client with no torrents', () => {
  const driver = new QBittorrentDriver();
  let token: string;
  let clientId: string;

  test.beforeAll(async () => {
    test.setTimeout(120_000);

    token = await loginAndGetToken();

    // Clear leftover clients from other specs.
    const existing = await listDownloadClients(token);
    for (const client of existing) {
      await deleteDownloadClient(token, client.id);
    }

    // The job runs on demand, so the schedule does not matter.
    const dcCurrent = await (await getDownloadCleanerConfig(token)).json();
    await updateDownloadCleanerConfig(token, {
      enabled: true,
      cronExpression: dcCurrent.cronExpression || '0 0 * * * ?',
      useAdvancedScheduling: dcCurrent.useAdvancedScheduling ?? false,
      ignoredDownloads: [],
    });

    mkdirShared(HOST_DOWNLOADS);

    // No decoy torrent here. An empty client is the subject of this spec.
    await driver.ready();
    await driver.clearAllTorrents();

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
    clientId = created.id;
  });

  test.beforeEach(async () => {
    resetDirectory(HOST_SCAN_DIR);
    mkdirShared(HOST_ORPHANED_DIR);
    await driver.clearAllTorrents();

    // A retry can leave the last test's sibling client behind.
    const existing = await listDownloadClients(token);
    for (const client of existing) {
      if (client.id !== clientId) {
        await deleteDownloadClient(token, client.id);
      }
    }
  });

  const configureOrphanedFiles = async (
    downloadClientId: string,
    overrides: Partial<OrphanedFilesConfigRequest> = {},
  ): Promise<void> => {
    const config: OrphanedFilesConfigRequest = {
      enabled: true,
      scanDirectories: [APP_SCAN_DIR],
      orphanedDirectory: APP_ORPHANED_DIR,
      excludePatterns: [],
      minFileAgeHours: 0,
      purgeAfterHours: null,
      ...overrides,
    };
    const res = await updateOrphanedFilesConfig(token, downloadClientId, config);
    expect(res.ok, `updateOrphanedFilesConfig: ${res.status}`).toBe(true);
  };

  test('Empty client → the orphan is moved to the orphaned directory', async () => {
    test.setTimeout(60_000);

    const orphan = writeOrphanFile(HOST_SCAN_DIR, 'orphan.mkv');
    await configureOrphanedFiles(clientId);

    await triggerAndSettle(token);
    await waitForCondition(
      () => existsSync(join(HOST_ORPHANED_DIR, 'orphan.mkv')),
      15_000,
      'orphan.mkv moved into the orphaned directory',
    );
    expect(existsSync(orphan)).toBe(false);
  });

  test('Empty client → aged entries in the orphaned directory are purged', async () => {
    test.setTimeout(60_000);

    const aged = writeOrphanFile(HOST_ORPHANED_DIR, 'aged.bin');
    backdateRecursive(aged, 25);
    await configureOrphanedFiles(clientId, { purgeAfterHours: 24 });

    await triggerAndSettle(token);
    await waitForCondition(() => !existsSync(aged), 15_000, `purge of ${aged}`);
  });

  test('Empty client → MinFileAgeHours still protects a fresh entry', async () => {
    test.setTimeout(60_000);

    const fresh = writeOrphanFile(HOST_SCAN_DIR, 'fresh.bin');
    await configureOrphanedFiles(clientId, { minFileAgeHours: 1 });

    const res = await triggerJob(token, 'DownloadCleaner');
    expect(res.ok, `triggerJob: ${res.status}`).toBe(true);
    // A negative assertion needs a window well past the real move time.
    // A short wait lets a broken build race the check and pass.
    await new Promise((r) => setTimeout(r, 20_000));

    expect(existsSync(fresh)).toBe(true);
    expect(readdirSync(HOST_ORPHANED_DIR).length).toBe(0);
  });

  test('Empty client is scanned while an unreachable sibling is left alone', async () => {
    test.setTimeout(120_000);

    resetDirectory(HOST_SIBLING_SCAN_DIR);
    mkdirShared(HOST_SIBLING_ORPHANED_DIR);

    const orphan = writeOrphanFile(HOST_SCAN_DIR, 'orphan.mkv');
    const realDownload = writeOrphanFile(HOST_SIBLING_SCAN_DIR, 'real-download.mkv', 'real');

    await configureOrphanedFiles(clientId);

    const createRes = await createDownloadClient(token, {
      enabled: true,
      name: 'qBittorrent unreachable sibling',
      typeName: 'qBittorrent',
      type: 'Torrent',
      // Nothing listens on port 1, so the qBit client fails to connect.
      host: 'http://127.0.0.1:1',
      username: 'admin',
      password: 'adminadmin',
      downloadDirectorySource: '/downloads',
      downloadDirectoryTarget: APP_SIBLING_SCAN_DIR,
    });
    expect(createRes.ok, `createDownloadClient: ${createRes.status}`).toBe(true);
    const sibling = await createRes.json();

    await configureOrphanedFiles(sibling.id, {
      scanDirectories: [APP_SIBLING_SCAN_DIR],
      orphanedDirectory: APP_SIBLING_ORPHANED_DIR,
    });

    await triggerAndSettle(token);
    await waitForCondition(
      () => existsSync(join(HOST_ORPHANED_DIR, 'orphan.mkv')),
      15_000,
      'the empty client orphan moved',
    );
    expect(existsSync(orphan)).toBe(false);

    expect(existsSync(realDownload)).toBe(true);
    expect(readdirSync(HOST_SIBLING_ORPHANED_DIR).length).toBe(0);
  });
});
