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
  updateUnlinkedConfig,
  triggerJob,
} from '../helpers/app-api';
import { DelugeDriver } from '../helpers/torrent-clients/deluge';
import {
  buildFolderTorrent,
  buildLargeSparseTorrent,
  chmodIgnoringEPERM,
  resetDirectory,
} from '../helpers/torrent-fixtures';

const HOST_DOWNLOADS = resolve(__dirname, '..', '..', 'test-data', 'downloads');
const SUBDIR = 'unlinked-large';
const HOST_DIR = join(HOST_DOWNLOADS, 'deluge', SUBDIR);
const CLIENT_SAVE_PATH = `/downloads/${SUBDIR}`;
const CATEGORY = 'dl-unlinked';
const TARGET = 'cleanuparr-unlinked';
const LARGE_SIZE = 3 * 1024 * 1024 * 1024;

const deluge = new DelugeDriver();

function sleep(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

async function waitForRegistered(infoHash: string, timeoutMs = 30_000): Promise<void> {
  const want = infoHash.toLowerCase();
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    if ((await deluge.listTorrents()).some((t) => t.hash.toLowerCase() === want)) return;
    await sleep(500);
  }
  throw new Error(`torrent ${infoHash} never registered with Deluge`);
}

/**
 * Unlinked cleanup of a torrent that holds a file larger than 2 GiB.
 *
 * The unlinked step gets the file list of a torrent with
 * `web.get_torrent_files`. Deluge gives the size of each file, and the offset of
 * each file, in bytes:
 *
 *   {"result": {"contents": {"data.bin": {"size": 3221225472, "offset": 0, …}}}}
 *
 * A file larger than 2 GiB makes these two values too large for a 32-bit
 * integer. The client then cannot read the file list. The unlinked step catches
 * the error and continues. The category of the torrent stays the same, and no
 * message tells the user.
 *
 * The small torrent is the control. The small torrent must move to the target
 * category in each build. This shows that the test setup is correct.
 */
test.describe.serial('Deluge unlinked cleanup with a large torrent', () => {
  let token: string;
  let clientId: string;
  let smallHash: string;
  let largeHash: string;

  test.beforeAll(async () => {
    test.setTimeout(180_000);

    token = await loginAndGetToken();
    for (const c of await listDownloadClients(token)) {
      await deleteDownloadClient(token, c.id);
    }

    const dc = await (await getDownloadCleanerConfig(token)).json();
    await updateDownloadCleanerConfig(token, {
      enabled: true,
      cronExpression: dc.cronExpression || '0 0 * * * ?',
      useAdvancedScheduling: dc.useAdvancedScheduling ?? false,
      ignoredDownloads: [],
    });

    mkdirSync(HOST_DOWNLOADS, { recursive: true });
    resetDirectory(HOST_DIR);
    chmodIgnoringEPERM(HOST_DIR, 0o777);
    await deluge.ready();
    await deluge.clearAllTorrents();
  });

  test.afterAll(async () => {
    await deluge.clearAllTorrents().catch(() => {});
    if (clientId) {
      await deleteDownloadClient(token, clientId).catch(() => {});
    }
  });

  test('sets up a small torrent and a torrent larger than 2 GiB', async () => {
    test.setTimeout(180_000);

    const small = buildFolderTorrent(HOST_DIR, 'unlinked-small', 32_768);
    const large = buildLargeSparseTorrent(HOST_DIR, 'unlinked-large-file', LARGE_SIZE);
    smallHash = small.infoHash;
    largeHash = large.infoHash;

    for (const fx of [small, large]) {
      await deluge.addSeedingTorrent({
        metainfo: fx.metainfo,
        savePath: CLIENT_SAVE_PATH,
        category: CATEGORY,
        name: fx.name,
        infoHash: fx.infoHash,
      });
      await waitForRegistered(fx.infoHash);
    }

    const createRes = await createDownloadClient(token, {
      enabled: true,
      name: 'Deluge large torrent e2e',
      typeName: deluge.typeName,
      type: 'Torrent',
      host: deluge.cleanuparrHost,
      username: deluge.username,
      password: deluge.password,
      downloadDirectorySource: '/downloads',
      downloadDirectoryTarget: '/e2e-downloads/deluge',
    });
    expect(createRes.ok, `createDownloadClient: ${createRes.status}`).toBe(true);
    clientId = (await createRes.json()).id;

    const cfg = await updateUnlinkedConfig(token, clientId, {
      enabled: true,
      targetCategory: TARGET,
      useTag: false,
      ignoredRootDirs: [],
      categories: [CATEGORY],
    });
    expect(cfg.ok, `updateUnlinkedConfig: ${cfg.status}`).toBe(true);

    expect(await deluge.getTorrentLabel(smallHash)).toBe(CATEGORY);
    expect(await deluge.getTorrentLabel(largeHash)).toBe(CATEGORY);
  });

  test('moves both torrents to the unlinked category', async () => {
    test.setTimeout(180_000);

    const trig = await triggerJob(token, 'DownloadCleaner');
    expect(trig.ok, `triggerJob: ${trig.status}`).toBe(true);
    await sleep(13_000); // Wait for the 10 s Arr sync delay of the job, and for the cleanup.

    expect(await deluge.getTorrentLabel(smallHash), 'the small torrent did not move').toBe(TARGET);
    expect(await deluge.getTorrentLabel(largeHash), 'the large torrent did not move').toBe(TARGET);
  });
});
