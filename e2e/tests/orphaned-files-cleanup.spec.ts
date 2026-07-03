import { test, expect } from '@playwright/test';
import { existsSync, mkdirSync, readdirSync } from 'node:fs';
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
import { ALL_CLIENTS, TorrentClientFixture } from './helpers/torrent-clients';
import {
  buildFolderTorrent,
  buildMultiFileTorrent,
  buildSingleFileTorrent,
  chmodIgnoringEPERM,
  resetDirectory,
  GeneratedTorrent,
} from './helpers/torrent-fixtures';

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
 * Orphaned files cleanup e2e — exercises the full pipeline for every
 * supported download client across the three torrent content layouts:
 *
 *   1. dir-single-file  — a folder containing one file
 *   2. dir-two-files    — a folder containing two files
 *   3. single-file      — one file with no containing folder
 *
 * For each client and layout the spec pre-creates two torrents whose data
 * lives in /e2e-downloads/<client>/, deletes one through the client's API
 * (leaving its data on disk to become a real orphan), triggers the
 * DownloadCleaner, then asserts the surviving torrent's content is untouched
 * and the orphan's content was moved into the orphaned directory. This
 * verifies each client resolves claimed paths correctly for every layout —
 * the regression behind issue #652.
 *
 * The downloads volume is bind-mounted at /e2e-downloads (app) / /downloads
 * (client), remapped via DownloadDirectorySource/Target so content paths
 * resolve into the scan directory.
 */

const HOST_DOWNLOADS = resolve(__dirname, '..', 'test-data', 'downloads');
const CLIENT_DOWNLOADS = '/downloads';
const APP_DOWNLOADS = '/e2e-downloads';

interface BuiltLayout {
  fx: GeneratedTorrent;
  /** The top-level on-disk entry (folder for dir layouts, file for single-file). */
  entryName: string;
  /** A file that must survive under the scan dir when the torrent is kept. */
  contentFileRel: string;
}

interface Layout {
  key: string;
  build: (scanDir: string, base: string) => BuiltLayout;
}

const LAYOUTS: Layout[] = [
  {
    key: 'dir-single-file',
    build: (scanDir, base) => {
      const fx = buildFolderTorrent(scanDir, base);
      return { fx, entryName: base, contentFileRel: join(base, 'data.bin') };
    },
  },
  {
    key: 'dir-two-files',
    build: (scanDir, base) => {
      const fx = buildMultiFileTorrent(scanDir, base, [{ filename: 'file1.bin' }, { filename: 'file2.bin' }]);
      return { fx, entryName: base, contentFileRel: join(base, 'file1.bin') };
    },
  },
  {
    key: 'single-file',
    build: (scanDir, base) => {
      const fileName = `${base}.bin`;
      const fx = buildSingleFileTorrent(scanDir, fileName);
      return { fx, entryName: fileName, contentFileRel: fileName };
    },
  },
];

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

test.describe.serial('Orphaned files cleanup', () => {
  let token: string;

  test.beforeAll(async () => {
    token = await loginAndGetToken();

    // Reset all existing download clients so the spec starts from a clean slate.
    const existing = await listDownloadClients(token);
    for (const client of existing) {
      await deleteDownloadClient(token, client.id);
    }

    // Enable the global download cleaner. Schedule is irrelevant since we
    // trigger the job manually.
    const dcCurrent = await (await getDownloadCleanerConfig(token)).json();
    await updateDownloadCleanerConfig(token, {
      enabled: true,
      cronExpression: dcCurrent.cronExpression || '0 0 * * * ?',
      useAdvancedScheduling: dcCurrent.useAdvancedScheduling ?? false,
      ignoredDownloads: [],
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
  const describeFn = fixture.enabled ? test.describe.serial : test.describe.skip;
  const dirs = clientDirs(slug);

  describeFn(`${driver.typeName}`, () => {
    let clientId: string;

    test.beforeAll(async () => {
      // Wait for the client's HTTP surface (slowest step on a cold start) and
      // register it with Cleanuparr once for all layout scenarios.
      await driver.ready();

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
      clientId = (await createRes.json()).id;

      const ofcRes = await updateOrphanedFilesConfig(getToken(), clientId, {
        enabled: true,
        scanDirectories: [dirs.appScanDir],
        orphanedDirectory: dirs.appOrphanedDir,
        minFileAgeHours: 0,
      });
      expect(ofcRes.status).toBe(200);
    });

    for (const layout of LAYOUTS) {
      test(`${layout.key}: keeps active torrent, moves orphan`, async () => {
        test.setTimeout(180_000);

        // Fresh scan dir so a previous layout/run doesn't bleed in.
        resetDirectory(dirs.hostScanDir);
        mkdirSync(dirs.hostOrphanedDir, { recursive: true });
        chmodIgnoringEPERM(dirs.hostOrphanedDir, 0o777);

        // Wipe client state — the session survives across layouts and would
        // otherwise reject re-adding a torrent or leave stale claims.
        await driver.clearAllTorrents();

        const keep = layout.build(dirs.hostScanDir, `keep-${slug}-${layout.key}`);
        const orphan = layout.build(dirs.hostScanDir, `orphan-${slug}-${layout.key}`);

        await driver.addTorrent({
          metainfo: keep.fx.metainfo,
          savePath: dirs.clientSavePath,
          name: keep.fx.name,
          infoHash: keep.fx.infoHash,
        });
        await driver.addTorrent({
          metainfo: orphan.fx.metainfo,
          savePath: dirs.clientSavePath,
          name: orphan.fx.name,
          infoHash: orphan.fx.infoHash,
        });

        await waitForTorrents(driver, [keep.fx.infoHash, orphan.fx.infoHash]);

        // Delete the orphan torrent from the client while preserving its data.
        await driver.deleteTorrent(orphan.fx.infoHash);

        const afterList = await driver.listTorrents();
        const afterHashes = new Set(afterList.map((t) => t.hash.toLowerCase()));
        expect(afterHashes.has(keep.fx.infoHash.toLowerCase())).toBe(true);
        expect(afterHashes.has(orphan.fx.infoHash.toLowerCase())).toBe(false);
        expect(existsSync(join(dirs.hostScanDir, orphan.entryName))).toBe(true);

        const trig = await triggerJob(getToken(), 'DownloadCleaner');
        expect(trig.ok, `triggerJob: ${trig.status}`).toBe(true);

        const moved = await waitForOrphanMove(dirs.hostOrphanedDir, orphan.entryName);

        // Kept torrent's content survives in place.
        expect(existsSync(join(dirs.hostScanDir, keep.contentFileRel))).toBe(true);
        // Orphan is gone from the scan dir and now lives under the orphaned dir.
        expect(existsSync(join(dirs.hostScanDir, orphan.entryName))).toBe(false);
        expect(existsSync(join(dirs.hostOrphanedDir, moved))).toBe(true);
      });
    }
  });
}
