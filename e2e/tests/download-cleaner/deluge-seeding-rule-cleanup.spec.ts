import { test, expect } from '@playwright/test';
import { existsSync } from 'node:fs';
import { join, resolve } from 'node:path';
import {
  loginAndGetToken,
  createDownloadClient,
  listDownloadClients,
  deleteDownloadClient,
  updateDownloadCleanerConfig,
  getDownloadCleanerConfig,
  createSeedingRule,
  triggerJob,
} from '../helpers/app-api';
import { DelugeDriver } from '../helpers/torrent-clients/deluge';
import { buildFolderTorrent, chmodIgnoringEPERM, resetDirectory } from '../helpers/torrent-fixtures';
import { mkdirShared } from '../helpers/shared-volume';

const HOST_DOWNLOADS = resolve(__dirname, '..', '..', 'test-data', 'downloads');
const DELUGE_DOWNLOADS = join(HOST_DOWNLOADS, 'deluge');
const CLIENT_DOWNLOADS = '/downloads';
const CATEGORY = 'dl-seed';
const ANNOUNCE = 'http://127.0.0.1:6969/announce';

const deluge = new DelugeDriver();

function sleep(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

// A torrent must seed and hold the category before the rule can match it.
async function waitForSeeding(infoHash: string, timeoutMs = 30_000): Promise<void> {
  const start = Date.now();
  let state: string | undefined;
  let label: string | undefined;
  while (Date.now() - start < timeoutMs) {
    state = await deluge.getTorrentState(infoHash);
    label = await deluge.getTorrentLabel(infoHash);
    if (state === 'Seeding' && label === CATEGORY) return;
    await sleep(500);
  }
  throw new Error(`torrent ${infoHash} is in state ${state} with label ${label}`);
}

async function stillPresent(infoHashes: string[]): Promise<string[]> {
  const present = (await deluge.listTorrents()).map((t) => t.hash.toLowerCase());
  return infoHashes.filter((h) => present.includes(h.toLowerCase()));
}

/**
 * Seeding rule cleanup with a live Deluge.
 *
 * A seeding rule is the only path that calls `DelugeClient.DeleteTorrents`.
 * This is the scenario of issue #705. Deluge answers `core.remove_torrents`
 * with `{"result": [], "error": null, "id": 1}`. The result holds the hashes
 * that Deluge did not remove. The result is empty after a success.
 *
 * Deluge removes the torrent before it sends the response. A client that cannot
 * read the response deletes the first torrent. The error then stops
 * `CleanDownloadsAsync`. Each torrent that remains stays in the client, and no
 * `DownloadCleaned` event happens. Two torrents and one cleanup cycle show this
 * behavior.
 */
test.describe.serial('Deluge seeding rule cleanup', () => {
  let token: string;
  let clientId: string;
  const hashes: string[] = [];
  const contentPaths: string[] = [];

  test.beforeAll(async () => {
    test.setTimeout(120_000);

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

    mkdirShared(HOST_DOWNLOADS);
    resetDirectory(DELUGE_DOWNLOADS);
    await deluge.ready();
    await deluge.clearAllTorrents();
  });

  test.afterAll(async () => {
    await deluge.clearAllTorrents().catch(() => {});
    if (clientId) {
      await deleteDownloadClient(token, clientId).catch(() => {});
    }
  });

  test('sets up two seeding torrents and a max-seed-time rule', async () => {
    test.setTimeout(120_000);

    chmodIgnoringEPERM(DELUGE_DOWNLOADS, 0o777);

    for (const suffix of ['a', 'b']) {
      const fx = buildFolderTorrent(DELUGE_DOWNLOADS, `seed-rule-${suffix}`, 32_768, ANNOUNCE);
      await deluge.addSeedingTorrent({
        metainfo: fx.metainfo,
        savePath: CLIENT_DOWNLOADS,
        category: CATEGORY,
        name: fx.name,
        infoHash: fx.infoHash,
      });
      await waitForSeeding(fx.infoHash);
      hashes.push(fx.infoHash);
      contentPaths.push(fx.contentPath);
    }

    const createRes = await createDownloadClient(token, {
      enabled: true,
      name: 'Deluge seeding rule e2e',
      typeName: deluge.typeName,
      type: 'Torrent',
      host: deluge.cleanuparrHost,
      username: deluge.username,
      password: deluge.password,
    });
    expect(createRes.status).toBeGreaterThanOrEqual(200);
    expect(createRes.status).toBeLessThan(300);
    clientId = (await createRes.json()).id;

    // A maxSeedTime of 0 cleans a torrent as soon as it seeds. The test does
    // not wait for a real seed time.
    const ruleRes = await createSeedingRule(token, clientId, {
      name: 'deluge max seed time',
      categories: [CATEGORY],
      trackerPatterns: [],
      tagsAny: [],
      tagsAll: [],
      privacyType: 'Both',
      maxRatio: -1,
      minSeedTime: 0,
      maxSeedTime: 0,
      minSeeders: 0,
      maxInactiveDays: -1,
      deleteSourceFiles: true,
    });
    expect(ruleRes.status).toBe(201);

    expect(await stillPresent(hashes)).toHaveLength(2);
  });

  test('removes every matching torrent and its files in a single cycle', async () => {
    test.setTimeout(180_000);

    const trig = await triggerJob(token, 'DownloadCleaner');
    expect(trig.ok, `triggerJob: ${trig.status}`).toBe(true);
    await sleep(10_000); // The job waits 10 s for the Arr queue sync.

    // One cycle must clean each torrent. The poll waits for that one cycle only.
    await expect
      .poll(() => stillPresent(hashes), {
        message: 'torrents survived the cleanup cycle',
        timeout: 60_000,
        intervals: [1_000],
      })
      .toEqual([]);
    for (const path of contentPaths) {
      expect(existsSync(path), `torrent data was not deleted: ${path}`).toBe(false);
    }
  });
});
