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
import { CleanuparrApi } from '../helpers/api';
import { UTorrentDriver } from '../helpers/torrent-clients/utorrent';
import { buildFolderTorrent, chmodIgnoringEPERM, resetDirectory } from '../helpers/torrent-fixtures';
import { mkdirShared } from '../helpers/shared-volume';

const HOST_DOWNLOADS = resolve(__dirname, '..', '..', 'test-data', 'downloads');
const UTORRENT_DOWNLOADS = join(HOST_DOWNLOADS, 'utorrent');
const CLIENT_DOWNLOADS = '/downloads';
const ANNOUNCE = 'http://host.docker.internal:6969/announce';

const utorrent = new UTorrentDriver();

function sleep(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

async function waitForSeeding(infoHash: string, category: string, timeoutMs = 30_000): Promise<void> {
  const start = Date.now();
  let status: number | undefined;
  let dateCompleted: number | undefined;
  let label: string | undefined;
  while (Date.now() - start < timeoutMs) {
    const row = await utorrent.getStatusAndCompleted(infoHash);
    status = row?.status;
    dateCompleted = row?.dateCompleted;
    label = await utorrent.getTorrentLabel(infoHash);
    if (row && (row.status & 1) !== 0 && (row.status & 8) !== 0 && row.dateCompleted > 0 && label === category) {
      return;
    }
    await sleep(500);
  }
  throw new Error(`torrent ${infoHash} has status ${status}, completed ${dateCompleted}, with label ${label}`);
}

async function isPresent(infoHash: string): Promise<boolean> {
  const want = infoHash.toLowerCase();
  return (await utorrent.listTorrents()).some((t) => t.hash.toLowerCase() === want);
}

async function countEvents(api: CleanuparrApi, eventType: string, infoHash: string): Promise<number> {
  const res = await api.events.list({ eventType, page: 1, pageSize: 500 });
  expect(res.status, 'events query failed').toBe(200);
  const body: { items?: Array<{ itemHash?: string }> } = await res.json();
  const want = infoHash.toLowerCase();
  return (body.items ?? []).filter((e) => (e.itemHash ?? '').toLowerCase() === want).length;
}

/** A stopped finished torrent must still reach the seeding rules. */
test.describe.serial('uTorrent stopped seeding rule', () => {
  let token: string;
  let api: CleanuparrApi;
  let clientId: string | undefined;

  async function addSeedingTorrentFx(suffix: string, category: string) {
    chmodIgnoringEPERM(UTORRENT_DOWNLOADS, 0o777);
    const fx = buildFolderTorrent(UTORRENT_DOWNLOADS, `stopped-seed-${suffix}`, 32_768, ANNOUNCE);
    await utorrent.addSeedingTorrent({
      metainfo: fx.metainfo,
      savePath: CLIENT_DOWNLOADS,
      category,
      name: fx.name,
      infoHash: fx.infoHash,
    });
    await waitForSeeding(fx.infoHash, category);
    return fx;
  }

  async function addStoppedTorrent(suffix: string, category: string) {
    const fx = await addSeedingTorrentFx(suffix, category);
    await utorrent.stop(fx.infoHash);
    expect(await utorrent.isStopped(fx.infoHash)).toBe(true);
    const row = await utorrent.getStatusAndCompleted(fx.infoHash);
    expect(row!.status).toBe(128 | 8);
    expect(row!.dateCompleted).toBeGreaterThan(0);
    return fx;
  }

  async function createClientWithRule(category: string, action: 'Delete' | 'Stop'): Promise<void> {
    const createRes = await createDownloadClient(token, {
      enabled: true,
      name: `uTorrent stopped ${action} e2e`,
      typeName: utorrent.typeName,
      host: utorrent.cleanuparrHost,
      username: utorrent.username,
      password: utorrent.password,
      downloadDirectorySource: '/downloads',
      downloadDirectoryTarget: '/e2e-downloads/utorrent',
    });
    expect(createRes.status).toBeGreaterThanOrEqual(200);
    expect(createRes.status).toBeLessThan(300);
    clientId = (await createRes.json()).id;

    // A maxSeedTime of 0 matches as soon as the torrent is finished.
    const ruleRes = await createSeedingRule(token, clientId!, {
      name: `utorrent stopped ${action}`,
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
    resetDirectory(UTORRENT_DOWNLOADS);
    await utorrent.ready();
    await utorrent.clearAllTorrents();
  });

  test.afterEach(async () => {
    await utorrent.clearAllTorrents().catch(() => {});
    if (clientId) {
      await deleteDownloadClient(token, clientId).catch(() => {});
      clientId = undefined;
    }
  });

  test('a Delete rule removes a stopped finished torrent and its files', async () => {
    test.setTimeout(180_000);

    const category = 'ut-stopped-del';
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

    const category = 'ut-stopped-stop';
    const fx = await addStoppedTorrent('stop', category);
    const control = await addSeedingTorrentFx('stop-control', category);
    const before = await countEvents(api, 'DownloadStopped', fx.infoHash);
    const controlBefore = await countEvents(api, 'DownloadStopped', control.infoHash);
    await createClientWithRule(category, 'Stop');

    const trig = await triggerJob(token, 'DownloadCleaner');
    expect(trig.ok, `triggerJob: ${trig.status}`).toBe(true);
    await sleep(10_000); // The job waits 10 s for the Arr queue sync.

    // The control's event proves the run evaluated this category.
    await expect
      .poll(() => countEvents(api, 'DownloadStopped', control.infoHash), {
        message: 'the Stop rule never stopped the active control torrent',
        timeout: 60_000,
        intervals: [1_000],
      })
      .toBeGreaterThan(controlBefore);
    expect(await utorrent.isStopped(control.infoHash)).toBe(true);

    expect(await countEvents(api, 'DownloadStopped', fx.infoHash)).toBe(before);
    expect(await isPresent(fx.infoHash)).toBe(true);
    expect(await utorrent.isStopped(fx.infoHash)).toBe(true);
  });
});
