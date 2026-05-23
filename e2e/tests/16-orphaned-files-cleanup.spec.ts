import { test, expect } from '@playwright/test';
import { chmodSync, existsSync, mkdirSync, readdirSync } from 'node:fs';
import { join, resolve } from 'node:path';
import {
  loginAndGetToken,
  createDownloadClient,
  listDownloadClients,
  deleteDownloadClient,
  updateDownloadCleanerConfig,
  getDownloadCleanerConfig,
  updateOrphanedFilesConfig,
  updateOrphanedFilesClientConfig,
  triggerJob,
} from './helpers/app-api';
import { ALL_CLIENTS, TorrentClientFixture } from './helpers/torrent-clients';
import { buildFolderTorrent, resetDirectory } from './helpers/torrent-fixtures';

async function waitForTorrents(
  driver: { listTorrents(): Promise<Array<{ hash: string }>> },
  expectedHashes: string[],
  timeoutMs = 15_000,
): Promise<void> {
  const want = new Set(expectedHashes.map((h) => h.toLowerCase()));
  const start = Date.now();
  let last: Set<string> = new Set();
  while (Date.now() - start < timeoutMs) {
    const list = await driver.listTorrents();
    last = new Set(list.map((t) => t.hash.toLowerCase()));
    if ([...want].every((h) => last.has(h))) return;
    await new Promise((r) => setTimeout(r, 500));
  }
  const missing = [...want].filter((h) => !last.has(h));
  throw new Error(`Torrents missing after ${timeoutMs}ms: ${missing.join(', ')} (saw [${[...last].join(', ')}])`);
}

async function waitForOrphanMove(dir: string, expectedName: string, timeoutMs = 45_000): Promise<string> {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    if (existsSync(dir)) {
      const entries = readdirSync(dir);
      const moved = entries.find((e) => e === expectedName || e.startsWith(`${expectedName}_`));
      if (moved) return moved;
    }
    await new Promise((r) => setTimeout(r, 1000));
  }
  throw new Error(`Timed out waiting for orphan "${expectedName}" to appear under ${dir}`);
}

/**
 * Orphaned files cleaner e2e — exercises the full pipeline for every
 * supported download client:
 *
 *   1. configure the download cleaner globally (enabled, generous schedule)
 *   2. configure the orphaned files cleaner globally (no min age, no purge)
 *   3. spin up the client and pre-create two torrents whose data lives in
 *      /e2e-downloads/<client>/
 *   4. delete one of those torrents through the client's API while keeping
 *      data on disk → produces a real orphan
 *   5. trigger the DownloadCleaner job
 *   6. assert the surviving torrent's files are untouched and the orphan's
 *      files were moved into /e2e-downloads/<client>/orphaned/
 *
 * The downloads volume is bind-mounted at the same path inside every
 * container (`/e2e-downloads`) and on the host (`e2e/test-data/downloads`)
 * so the spec can assert directly against host paths without any
 * DownloadDirectorySource/Target remapping.
 */

const HOST_DOWNLOADS = resolve(__dirname, '..', 'test-data', 'downloads');
const CLIENT_DOWNLOADS = '/downloads';
const APP_DOWNLOADS = '/e2e-downloads';

function clientDirs(slug: string) {
  return {
    hostScanDir: join(HOST_DOWNLOADS, slug),
    hostOrphanedDir: join(HOST_DOWNLOADS, slug, 'orphaned'),
    clientSavePath: CLIENT_DOWNLOADS,
    appScanDir: `${APP_DOWNLOADS}/${slug}`,
    appOrphanedDir: `${APP_DOWNLOADS}/${slug}/orphaned`,
  };
}

const SLUG_BY_TYPE: Record<string, string> = {
  qBittorrent: 'qbittorrent',
  Transmission: 'transmission',
  Deluge: 'deluge',
  uTorrent: 'utorrent',
  rTorrent: 'rtorrent',
};

test.describe.serial('Orphaned files cleaner', () => {
  let token: string;

  test.beforeAll(async () => {
    token = await loginAndGetToken();

    // Reset all existing download clients so the spec starts from a clean slate.
    const existing = await listDownloadClients(token);
    for (const client of existing) {
      await deleteDownloadClient(token, client.id);
    }

    // Enable the global download cleaner + the global orphaned-files config.
    // Schedule is irrelevant since we trigger the job manually.
    const dcCurrent = await (await getDownloadCleanerConfig(token)).json();
    await updateDownloadCleanerConfig(token, {
      enabled: true,
      cronExpression: dcCurrent.cronExpression || '0 0 * * * ?',
      useAdvancedScheduling: dcCurrent.useAdvancedScheduling ?? false,
      ignoredDownloads: [],
    });

    await updateOrphanedFilesConfig(token, {
      excludePatterns: [],
      minFileAgeMinutes: 0,
      emptyAfterXDays: null,
    });

    mkdirSync(HOST_DOWNLOADS, { recursive: true });
  });

  for (const fixture of ALL_CLIENTS) {
    runClientScenario(fixture, () => token);
  }
});

function runClientScenario(fixture: TorrentClientFixture, getToken: () => string) {
  const { driver } = fixture;
  const slug = SLUG_BY_TYPE[driver.typeName];
  const describeFn = fixture.enabled ? test.describe : test.describe.skip;

  describeFn(`${driver.typeName}`, () => {
    let keep: { name: string; infoHash: string };
    let orphan: { name: string; infoHash: string };
    let clientId: string;
    const dirs = clientDirs(slug);

    test('configures client and produces an orphan', async () => {
      test.setTimeout(180_000);

      // Fresh per-client scan dir so a previous failed run doesn't bleed in.
      resetDirectory(dirs.hostScanDir);
      mkdirSync(dirs.hostOrphanedDir, { recursive: true });
      chmodSync(dirs.hostOrphanedDir, 0o777);

      const keepName = `keep-${slug}`;
      const orphanName = `orphan-${slug}`;
      const keepFx = buildFolderTorrent(dirs.hostScanDir, keepName);
      const orphanFx = buildFolderTorrent(dirs.hostScanDir, orphanName);
      keep = { name: keepName, infoHash: keepFx.infoHash };
      orphan = { name: orphanName, infoHash: orphanFx.infoHash };

      // Wait for the client's HTTP surface to come up. This is the slowest
      // step on a cold compose start.
      await driver.ready();

      // Wipe any torrents left over from a prior `make test` run — the
      // client's session is in a persistent config volume that survives
      // `make test` and would otherwise reject re-adding the same infohash.
      await driver.clearAllTorrents();

      const createRes = await createDownloadClient(getToken(), {
        enabled: true,
        name: `${driver.typeName} e2e`,
        typeName: driver.typeName,
        type: 'Torrent',
        host: driver.cleanuparrHost,
        username: driver.username ?? '',
        password: driver.password ?? '',
        downloadDirectorySource: dirs.clientSavePath,
        downloadDirectoryTarget: dirs.appScanDir,
      });
      expect(createRes.status).toBeGreaterThanOrEqual(200);
      expect(createRes.status).toBeLessThan(300);
      const createdClient = await createRes.json();
      clientId = createdClient.id;

      const ofcRes = await updateOrphanedFilesClientConfig(getToken(), clientId, {
        enabled: true,
        scanDirectories: [dirs.appScanDir],
        orphanedDirectory: dirs.appOrphanedDir,
      });
      expect(ofcRes.status).toBe(200);

      await driver.addTorrent({
        metainfo: keepFx.metainfo,
        savePath: dirs.clientSavePath,
        name: keepName,
        infoHash: keepFx.infoHash,
      });
      await driver.addTorrent({
        metainfo: orphanFx.metainfo,
        savePath: dirs.clientSavePath,
        name: orphanName,
        infoHash: orphanFx.infoHash,
      });

      // Some clients process `add` asynchronously — poll for both torrents
      // to become visible before continuing.
      await waitForTorrents(driver, [keep.infoHash, orphan.infoHash]);

      // Delete the orphan torrent from the client while preserving data.
      await driver.deleteTorrent(orphan.infoHash);

      // Verify orphan is gone from the client but still present on disk.
      const afterList = await driver.listTorrents();
      const afterHashes = new Set(afterList.map((t) => t.hash.toLowerCase()));
      expect(afterHashes.has(keep.infoHash.toLowerCase())).toBe(true);
      expect(afterHashes.has(orphan.infoHash.toLowerCase())).toBe(false);
      expect(existsSync(join(dirs.hostScanDir, orphanName))).toBe(true);

      // Trigger the cleaner. The job runs async on a worker thread; we poll
      // the filesystem for the expected outcome rather than sleeping.
      const trig = await triggerJob(getToken(), 'DownloadCleaner');
      expect(trig.ok, `triggerJob: ${trig.status}`).toBe(true);

      const moved = await waitForOrphanMove(dirs.hostOrphanedDir, orphanName);

      // Assert: kept torrent's folder survives in place.
      expect(existsSync(join(dirs.hostScanDir, keepName, 'data.bin'))).toBe(true);
      // Assert: orphan folder no longer at top of scan dir.
      expect(existsSync(join(dirs.hostScanDir, orphanName))).toBe(false);
      // Assert: orphan folder is under the orphanedDirectory, with its data intact.
      expect(existsSync(join(dirs.hostOrphanedDir, moved, 'data.bin'))).toBe(true);
    });
  });
}
