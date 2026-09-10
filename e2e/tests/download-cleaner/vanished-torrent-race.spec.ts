import { test, expect } from '@playwright/test';
import { join, resolve } from 'node:path';
import {
  loginAndGetToken,
  createDownloadClient,
  listDownloadClients,
  deleteDownloadClient,
  updateDownloadCleanerConfig,
  getDownloadCleanerConfig,
  getGeneralConfig,
  updateGeneralConfig,
  triggerJob,
} from '../helpers/app-api';
import { appLogsSince } from '../helpers/test-lifecycle';
import { QBittorrentDriver } from '../helpers/torrent-clients/qbittorrent';
import { buildSingleFileTorrent, resetDirectory } from '../helpers/torrent-fixtures';
import { mkdirShared } from '../helpers/shared-volume';

const HOST_DOWNLOADS = resolve(__dirname, '..', '..', 'test-data', 'downloads');
const CLIENT_DOWNLOADS = '/downloads';
const ANNOUNCE_HOST = 'http://127.0.0.1:6969/announce';
const CATEGORY = 'vanish-race';

/**
 * Enough torrents that the enumeration loop stays busy while the deleter runs.
 * The loop issues two requests per torrent, the deleter one.
 */
const TORRENT_COUNT = 120;

/** Deleting from the tail leaves the head of the list for the loop to finish. */
const DELETE_INTERVAL_MS = 10;

const qbit = new QBittorrentDriver();

function sleep(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

/** The seeding pass reads this filter, so a torrent only counts once it appears here. */
async function completedCount(): Promise<number> {
  const res = await fetch('http://localhost:8090/api/v2/torrents/info?filter=completed');
  if (!res.ok) {
    throw new Error(`qBittorrent completed list failed: ${res.status}`);
  }
  return ((await res.json()) as unknown[]).length;
}

/**
 * A torrent deleted between the bulk list call and its own per-hash call makes
 * qBittorrent answer 404, which the client library turns into a null list.
 *
 * Issue #779: that null reached a LINQ call and threw ArgumentNullException out
 * of GetSeedingDownloads, discarding every torrent already enriched and
 * skipping the client for the whole cleanup pass.
 */
test.describe.serial('Download Cleaner with torrents deleted mid-pass', () => {
  let token: string;
  let clientId: string | undefined;
  let originalLogLevel: unknown;
  let hashes: string[] = [];

  test.beforeAll(async () => {
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

    // The skip decision is only observable at debug level.
    const general = await getGeneralConfig(token);
    const log = general.log as Record<string, unknown>;
    originalLogLevel = log.level;
    await updateGeneralConfig(token, { ...general, log: { ...log, level: 'Debug' } });

    mkdirShared(HOST_DOWNLOADS);
  });

  test.afterAll(async () => {
    await qbit.clearAllTorrents().catch(() => {});
    if (clientId) {
      await deleteDownloadClient(token, clientId).catch(() => {});
    }
    const general = await getGeneralConfig(token);
    const log = general.log as Record<string, unknown>;
    await updateGeneralConfig(token, { ...general, log: { ...log, level: originalLogLevel } }).catch(() => {});
  });

  test('seeds a torrent set large enough to race', async () => {
    test.setTimeout(300_000);

    const dir = join(HOST_DOWNLOADS, 'qbittorrent');
    resetDirectory(dir);
    mkdirShared(dir);

    await qbit.ready();
    await qbit.clearAllTorrents();

    for (let i = 0; i < TORRENT_COUNT; i++) {
      const fx = buildSingleFileTorrent(dir, `vanish-race-${i}.bin`, 1024, ANNOUNCE_HOST);
      await qbit.addSeedingTorrent({
        metainfo: fx.metainfo,
        savePath: CLIENT_DOWNLOADS,
        category: CATEGORY,
        infoHash: fx.infoHash,
      });
    }

    await expect
      .poll(completedCount, {
        message: 'not every torrent reached the completed state',
        timeout: 120_000,
        intervals: [1_000],
      })
      .toBe(TORRENT_COUNT);

    hashes = (await qbit.listTorrents()).map((t) => t.hash);

    const createRes = await createDownloadClient(token, {
      enabled: true,
      name: 'qBittorrent vanish race e2e',
      typeName: qbit.typeName,
      type: 'Torrent',
      host: qbit.cleanuparrHost,
      username: qbit.username ?? '',
      password: qbit.password ?? '',
    });
    expect(createRes.status).toBeGreaterThanOrEqual(200);
    expect(createRes.status).toBeLessThan(300);
    clientId = (await createRes.json()).id;
  });

  test('skips the vanished torrents instead of aborting the pass', async () => {
    test.setTimeout(300_000);

    const since = new Date().toISOString();

    const trig = await triggerJob(token, 'DownloadCleaner');
    expect(trig.ok, `triggerJob: ${trig.status}`).toBe(true);

    // Deleting from the tail while the loop reads from the head is what opens
    // the window between the bulk list call and the per-hash calls.
    for (const hash of [...hashes].reverse()) {
      await qbit.deleteTorrent(hash).catch(() => {});
      await sleep(DELETE_INTERVAL_MS);
    }

    await expect
      .poll(() => appLogsSince(since).includes('torrent no longer exists in the download client'), {
        message: 'the race never landed: no torrent vanished between the list call and its per-hash calls',
        timeout: 60_000,
        intervals: [1_000],
      })
      .toBe(true);

    const logs = appLogsSince(since);
    expect(logs, 'the vanished torrent crashed the pass').not.toContain('ArgumentNullException');
    expect(logs, 'the cleanup pass aborted for the client').not.toContain(
      'Failed to get seeding downloads from download client',
    );
    // The crash discarded every torrent enriched before it, leaving the pass with nothing.
    expect(logs, 'the surviving torrents were discarded along with the vanished one').not.toContain(
      'No seeding downloads found',
    );
  });
});
