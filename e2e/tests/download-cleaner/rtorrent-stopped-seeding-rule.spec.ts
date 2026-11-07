import { test, expect } from '@playwright/test';
import { existsSync, linkSync, rmSync } from 'node:fs';
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
  updateUnlinkedConfig,
} from '../helpers/app-api';
import { CleanuparrApi } from '../helpers/api';
import { RTorrentDriver } from '../helpers/torrent-clients/rtorrent';
import { buildFolderTorrent, chmodIgnoringEPERM, resetDirectory } from '../helpers/torrent-fixtures';
import { mkdirShared } from '../helpers/shared-volume';

const HOST_DOWNLOADS = resolve(__dirname, '..', '..', 'test-data', 'downloads');
const RTORRENT_DOWNLOADS = join(HOST_DOWNLOADS, 'rtorrent');
const HARDLINK_DIR = join(RTORRENT_DOWNLOADS, 'hardlinks');
const CLIENT_DOWNLOADS = '/downloads';
const ANNOUNCE = 'http://127.0.0.1:6969/announce';

const rtorrent = new RTorrentDriver();

function sleep(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

async function waitForSeeding(infoHash: string, category: string, timeoutMs = 30_000): Promise<void> {
  const start = Date.now();
  let state: number | undefined;
  let complete: number | undefined;
  let label: string | undefined;
  while (Date.now() - start < timeoutMs) {
    state = await rtorrent.getState(infoHash);
    complete = await rtorrent.getComplete(infoHash);
    label = await rtorrent.getLabel(infoHash);
    if (state === 1 && complete === 1 && label === category) {
      return;
    }
    await sleep(500);
  }
  throw new Error(`torrent ${infoHash} is in state ${state}, complete ${complete}, with label ${label}`);
}

async function isPresent(infoHash: string): Promise<boolean> {
  const want = infoHash.toLowerCase();
  return (await rtorrent.listTorrents()).some((t) => t.hash.toLowerCase() === want);
}

async function countEvents(api: CleanuparrApi, eventType: string, infoHash: string): Promise<number> {
  const res = await api.events.list({ eventType, page: 1, pageSize: 500 });
  expect(res.status, 'events query failed').toBe(200);
  const body: { items?: Array<{ itemHash?: string }> } = await res.json();
  const want = infoHash.toLowerCase();
  return (body.items ?? []).filter((e) => (e.itemHash ?? '').toLowerCase() === want).length;
}

/** A finished torrent stopped with ruTorrent's Stop must still reach the seeding rules. */
test.describe.serial('rTorrent stopped seeding rule', () => {
  let token: string;
  let api: CleanuparrApi;
  let clientId: string | undefined;

  async function addStoppedTorrent(suffix: string, category: string) {
    chmodIgnoringEPERM(RTORRENT_DOWNLOADS, 0o777);
    const fx = buildFolderTorrent(RTORRENT_DOWNLOADS, `stopped-seed-${suffix}`, 32_768, ANNOUNCE);
    await rtorrent.addSeedingTorrent({
      metainfo: fx.metainfo,
      savePath: CLIENT_DOWNLOADS,
      category,
      name: fx.name,
      infoHash: fx.infoHash,
    });
    await waitForSeeding(fx.infoHash, category);
    await rtorrent.stopTorrent(fx.infoHash);
    expect(await rtorrent.getState(fx.infoHash)).toBe(0);
    expect(await rtorrent.getIsOpen(fx.infoHash)).toBe(0);
    expect(await rtorrent.getComplete(fx.infoHash)).toBe(1);
    return fx;
  }

  async function createClientWithRule(category: string, action: 'Delete' | 'Stop'): Promise<void> {
    const createRes = await createDownloadClient(token, {
      enabled: true,
      name: `rTorrent stopped ${action} e2e`,
      typeName: rtorrent.typeName,
      host: rtorrent.cleanuparrHost,
      username: rtorrent.username,
      password: rtorrent.password,
      downloadDirectorySource: '/downloads',
      downloadDirectoryTarget: '/e2e-downloads/rtorrent',
    });
    expect(createRes.status).toBeGreaterThanOrEqual(200);
    expect(createRes.status).toBeLessThan(300);
    clientId = (await createRes.json()).id;

    // A maxSeedTime of 0 matches as soon as the torrent is finished.
    const ruleRes = await createSeedingRule(token, clientId!, {
      name: `rtorrent stopped ${action}`,
      categories: [category],
      trackerPatterns: [],
      tagsAny: [],
      tagsAll: [],
      privacyType: 'Both',
      maxRatio: -1,
      minSeedTime: 0,
      maxSeedTime: 0,
      minSeeders: 0,
      maxInactiveDays: -1,
      action,
      deleteSourceFiles: action === 'Delete',
    });
    expect(ruleRes.status).toBe(201);
  }

  test.beforeAll(async () => {
    test.setTimeout(120_000);

    token = await loginAndGetToken();
    api = new CleanuparrApi({ token });
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

  test.afterEach(async () => {
    await rtorrent.clearAllTorrents().catch(() => {});
    rmSync(HARDLINK_DIR, { recursive: true, force: true });
    if (clientId) {
      await deleteDownloadClient(token, clientId).catch(() => {});
      clientId = undefined;
    }
  });

  test('a Delete rule removes a stopped finished torrent and its files', async () => {
    test.setTimeout(180_000);

    const category = 'rt-stopped-del';
    const fx = await addStoppedTorrent('delete', category);
    await createClientWithRule(category, 'Delete');

    const trig = await triggerJob(token, 'DownloadCleaner');
    expect(trig.ok, `triggerJob: ${trig.status}`).toBe(true);
    await sleep(10_000); // The job waits 10 s for the Arr queue sync.

    await expect
      .poll(() => isPresent(fx.infoHash), {
        message: 'stopped torrent survived the cleanup cycle',
        timeout: 60_000,
        intervals: [1_000],
      })
      .toBe(false);
    expect(existsSync(fx.contentPath), `torrent data was not deleted: ${fx.contentPath}`).toBe(false);
    expect(await countEvents(api, 'DownloadCleaned', fx.infoHash)).toBeGreaterThanOrEqual(1);
  });

  test('a Stop rule on an already stopped torrent emits no DownloadStopped event', async () => {
    test.setTimeout(180_000);

    const category = 'rt-stopped-stop';
    const fx = await addStoppedTorrent('stop', category);
    const before = await countEvents(api, 'DownloadStopped', fx.infoHash);
    await createClientWithRule(category, 'Stop');

    const trig = await triggerJob(token, 'DownloadCleaner');
    expect(trig.ok, `triggerJob: ${trig.status}`).toBe(true);
    // No event to poll for, so wait out the run.
    await sleep(20_000);

    expect(await countEvents(api, 'DownloadStopped', fx.infoHash)).toBe(before);
    expect(await isPresent(fx.infoHash)).toBe(true);
    expect(await rtorrent.getState(fx.infoHash)).toBe(0);
  });

  test('an unlinked config moves a closed unlinked torrent but keeps a hardlinked one', async () => {
    test.setTimeout(180_000);

    const category = 'rt-closed-unlinked';
    const target = 'cleanuparr-unlinked';
    const unlinked = await addStoppedTorrent('unlinked', category);
    const linked = await addStoppedTorrent('linked', category);
    mkdirShared(HARDLINK_DIR);
    linkSync(join(linked.contentPath, 'data.bin'), join(HARDLINK_DIR, 'data.bin'));

    const createRes = await createDownloadClient(token, {
      enabled: true,
      name: 'rTorrent stopped unlinked e2e',
      typeName: rtorrent.typeName,
      host: rtorrent.cleanuparrHost,
      username: rtorrent.username,
      password: rtorrent.password,
      downloadDirectorySource: '/downloads',
      downloadDirectoryTarget: '/e2e-downloads/rtorrent',
    });
    expect(createRes.ok, `createDownloadClient: ${createRes.status}`).toBe(true);
    clientId = (await createRes.json()).id;

    const cfg = await updateUnlinkedConfig(token, clientId!, {
      enabled: true,
      targetCategory: target,
      useTag: false,
      ignoredRootDirs: [],
      categories: [category],
    });
    expect(cfg.ok, `updateUnlinkedConfig: ${cfg.status}`).toBe(true);

    const trig = await triggerJob(token, 'DownloadCleaner');
    expect(trig.ok, `triggerJob: ${trig.status}`).toBe(true);
    await sleep(10_000); // The job waits 10 s for the Arr queue sync.

    await expect
      .poll(() => rtorrent.getLabel(unlinked.infoHash), {
        message: 'the closed unlinked torrent did not move',
        timeout: 60_000,
        intervals: [1_000],
      })
      .toBe(target);
    expect(await rtorrent.getLabel(linked.infoHash)).toBe(category);
    expect(await rtorrent.getState(unlinked.infoHash)).toBe(0);
    expect(await rtorrent.getState(linked.infoHash)).toBe(0);
  });
});
