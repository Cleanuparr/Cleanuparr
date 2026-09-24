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
import { RTorrentDriver } from '../helpers/torrent-clients/rtorrent';
import { buildFolderTorrent, chmodIgnoringEPERM, resetDirectory } from '../helpers/torrent-fixtures';
import { mkdirShared } from '../helpers/shared-volume';

const HOST_DOWNLOADS = resolve(__dirname, '..', '..', 'test-data', 'downloads');
const RTORRENT_DOWNLOADS = join(HOST_DOWNLOADS, 'rtorrent');
const CLIENT_DOWNLOADS = '/downloads';
const CATEGORY = 'rt-seed';
// GetDomain resolves 'http://127.0.0.1:6969/announce' to host '127.0.0.1'.
const ANNOUNCE = 'http://127.0.0.1:6969/announce';
const TRACKER_PATTERN = '127.0.0.1';

const rtorrent = new RTorrentDriver();

function sleep(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

// A torrent must finish its hash check and hold the category before the rule can match it.
async function waitForSeeding(infoHash: string, timeoutMs = 30_000): Promise<void> {
  const start = Date.now();
  let state: number | undefined;
  let complete: number | undefined;
  let label: string | undefined;
  while (Date.now() - start < timeoutMs) {
    state = await rtorrent.getState(infoHash);
    complete = await rtorrent.getComplete(infoHash);
    label = await rtorrent.getLabel(infoHash);
    if (state === 1 && complete === 1 && label === CATEGORY) return;
    await sleep(500);
  }
  throw new Error(`torrent ${infoHash} is in state ${state}, complete ${complete}, with label ${label}`);
}

async function stillPresent(infoHashes: string[]): Promise<string[]> {
  const present = (await rtorrent.listTorrents()).map((t) => t.hash.toLowerCase());
  return infoHashes.filter((h) => present.includes(h.toLowerCase()));
}

/**
 * Regression test for #807: rTorrent tracker patterns never matched.
 * The rule below matches only by tracker pattern, so it fails on main.
 */
test.describe.serial('rTorrent seeding rule tracker pattern cleanup', () => {
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
    resetDirectory(RTORRENT_DOWNLOADS);
    await rtorrent.ready();
    await rtorrent.clearAllTorrents();
  });

  test.afterAll(async () => {
    await rtorrent.clearAllTorrents().catch(() => {});
    if (clientId) {
      await deleteDownloadClient(token, clientId).catch(() => {});
    }
  });

  test('sets up a seeding torrent and a tracker-pattern rule', async () => {
    test.setTimeout(120_000);

    chmodIgnoringEPERM(RTORRENT_DOWNLOADS, 0o777);

    const fx = buildFolderTorrent(RTORRENT_DOWNLOADS, 'seed-rule-tracker', 32_768, ANNOUNCE);
    await rtorrent.addSeedingTorrent({
      metainfo: fx.metainfo,
      savePath: CLIENT_DOWNLOADS,
      category: CATEGORY,
      name: fx.name,
      infoHash: fx.infoHash,
    });
    await waitForSeeding(fx.infoHash);
    hashes.push(fx.infoHash);
    contentPaths.push(fx.contentPath);

    const createRes = await createDownloadClient(token, {
      enabled: true,
      name: 'rTorrent seeding rule e2e',
      typeName: rtorrent.typeName,
      type: 'Torrent',
      host: rtorrent.cleanuparrHost,
      username: rtorrent.username,
      password: rtorrent.password,
      downloadDirectorySource: '/downloads',
      downloadDirectoryTarget: '/e2e-downloads/rtorrent',
    });
    expect(createRes.status).toBeGreaterThanOrEqual(200);
    expect(createRes.status).toBeLessThan(300);
    clientId = (await createRes.json()).id;

    // Category alone would match every torrent; only trackerPatterns filters here.
    const ruleRes = await createSeedingRule(token, clientId, {
      name: 'rtorrent tracker pattern',
      categories: [CATEGORY],
      trackerPatterns: [TRACKER_PATTERN],
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

    expect(await stillPresent(hashes)).toHaveLength(1);
  });

  test('matches the tracker pattern and removes the torrent and its files', async () => {
    test.setTimeout(180_000);

    const trig = await triggerJob(token, 'DownloadCleaner');
    expect(trig.ok, `triggerJob: ${trig.status}`).toBe(true);
    await sleep(10_000); // The job waits 10 s for the Arr queue sync.

    await expect
      .poll(() => stillPresent(hashes), {
        message: 'torrent survived the cleanup cycle (tracker pattern rule did not match)',
        timeout: 60_000,
        intervals: [1_000],
      })
      .toEqual([]);
    for (const path of contentPaths) {
      expect(existsSync(path), `torrent data was not deleted: ${path}`).toBe(false);
    }
  });
});
