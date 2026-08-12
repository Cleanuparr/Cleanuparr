import { test, expect } from '@playwright/test';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
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
 * This is a regression test for issue #700. The test moves an orphaned
 * directory to an orphaned directory on a different filesystem.
 *
 * On Linux, `Directory.Move` does only a `rename(2)` operation. The rename
 * operation fails with the EXDEV error ("Invalid cross-device link") if the
 * source and the destination are on different mount points. Refer to
 * dotnet/runtime#31149.
 *
 * The app container has two different bind mounts: `/e2e-downloads` and
 * `/e2e-orphaned`. Two bind mounts cause the EXDEV error. The host directories
 * can be on the same filesystem.
 *
 * If the EXDEV error occurs, `File.Move` copies the file and then deletes the
 * source file. Thus the test moves a file only as an additional check. The
 * directory is the important part of the test.
 */

const HOST_DOWNLOADS = resolve(__dirname, '..', '..', 'test-data', 'downloads');
const HOST_ORPHANED_MOUNT = resolve(__dirname, '..', '..', 'test-data', 'orphaned-xdev');
const SLUG = 'qbittorrent-xdev';
const HOST_SCAN_DIR = join(HOST_DOWNLOADS, SLUG);
const APP_SCAN_DIR = `/e2e-downloads/${SLUG}`;
const APP_ORPHANED_DIR = '/e2e-orphaned';

const HOST_DECOY_PARENT = join(HOST_DOWNLOADS, 'qbittorrent');
const CLIENT_DECOY_PARENT = '/downloads';
const DECOY_NAME = '__cleanuparr_xdev_decoy__';

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

test.describe.serial('Orphaned files cleanup — cross-device move', () => {
  const driver = new QBittorrentDriver();
  let token: string;
  let clientId: string;

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
    mkdirSync(HOST_ORPHANED_MOUNT, { recursive: true });
    chmodIgnoringEPERM(HOST_ORPHANED_MOUNT, 0o777);

    await driver.ready();
    await driver.clearAllTorrents();

    mkdirSync(HOST_DECOY_PARENT, { recursive: true });
    chmodIgnoringEPERM(HOST_DECOY_PARENT, 0o777);
    const decoy = buildFolderTorrent(HOST_DECOY_PARENT, DECOY_NAME);
    await driver.addTorrent({
      metainfo: decoy.metainfo,
      savePath: CLIENT_DECOY_PARENT,
      name: DECOY_NAME,
      infoHash: decoy.infoHash,
    });
    await waitForCondition(
      async () => {
        const list = await driver.listTorrents();
        return list.some((t) => t.hash.toLowerCase() === decoy.infoHash.toLowerCase());
      },
      15_000,
      'decoy torrent registered in qBittorrent',
    );

    const createRes = await createDownloadClient(token, {
      enabled: true,
      name: 'qBittorrent cross-device',
      typeName: driver.typeName,
      type: 'Torrent',
      host: driver.cleanuparrHost,
      username: driver.username ?? '',
      password: driver.password ?? '',
      downloadDirectorySource: '/downloads',
      downloadDirectoryTarget: APP_SCAN_DIR,
    });
    expect(createRes.ok, `createDownloadClient: ${createRes.status}`).toBe(true);
    clientId = (await createRes.json()).id;
  });

  test('moves an orphaned directory onto a different mount', async () => {
    test.setTimeout(60_000);

    resetDirectory(HOST_SCAN_DIR);
    resetDirectory(HOST_ORPHANED_MOUNT);

    const orphanRoot = join(HOST_SCAN_DIR, 'xdev-show');
    const orphanTree = join(orphanRoot, 'season 1');
    mkdirSync(orphanTree, { recursive: true });
    writeFileSync(join(orphanTree, 'episode.mkv'), 'episode');
    writeFileSync(join(HOST_SCAN_DIR, 'loose.bin'), 'loose');
    chmodIgnoringEPERM(HOST_SCAN_DIR, 0o777);
    chmodIgnoringEPERM(orphanRoot, 0o777);
    chmodIgnoringEPERM(orphanTree, 0o777);

    const res = await updateOrphanedFilesConfig(token, clientId, {
      enabled: true,
      scanDirectories: [APP_SCAN_DIR],
      orphanedDirectory: APP_ORPHANED_DIR,
      excludePatterns: [],
      minFileAgeHours: 0,
      purgeAfterHours: null,
    });
    expect(res.ok, `updateOrphanedFilesConfig: ${res.status}`).toBe(true);

    const trigger = await triggerJob(token, 'DownloadCleaner');
    expect(trigger.ok, `triggerJob: ${trigger.status}`).toBe(true);

    const movedEpisode = join(HOST_ORPHANED_MOUNT, 'xdev-show', 'season 1', 'episode.mkv');
    await waitForCondition(() => existsSync(movedEpisode), 30_000, `cross-device move of ${movedEpisode}`);

    expect(readFileSync(movedEpisode, 'utf8')).toBe('episode');
    expect(existsSync(join(HOST_SCAN_DIR, 'xdev-show'))).toBe(false);

    const movedFile = join(HOST_ORPHANED_MOUNT, 'loose.bin');
    await waitForCondition(() => existsSync(movedFile), 30_000, `cross-device move of ${movedFile}`);
    expect(existsSync(join(HOST_SCAN_DIR, 'loose.bin'))).toBe(false);
  });
});
