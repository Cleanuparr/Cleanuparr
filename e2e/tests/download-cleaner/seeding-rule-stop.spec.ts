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
import { QBittorrentDriver } from '../helpers/torrent-clients/qbittorrent';
import { TransmissionDriver } from '../helpers/torrent-clients/transmission';
import { DelugeDriver } from '../helpers/torrent-clients/deluge';
import { UTorrentDriver } from '../helpers/torrent-clients/utorrent';
import { buildFolderTorrent, resetDirectory } from '../helpers/torrent-fixtures';
import { mkdirShared } from '../helpers/shared-volume';

const HOST_DOWNLOADS = resolve(__dirname, '..', '..', 'test-data', 'downloads');
const CLIENT_DOWNLOADS = '/downloads';
const ANNOUNCE_HOST = 'http://127.0.0.1:6969/announce';
const ANNOUNCE_BRIDGE = 'http://host.docker.internal:6969/announce';

const qbit = new QBittorrentDriver();
const transmission = new TransmissionDriver();
const deluge = new DelugeDriver();
const utorrent = new UTorrentDriver();

function sleep(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

interface DriverLike {
  readonly typeName: string;
  readonly cleanuparrHost: string;
  readonly username?: string;
  readonly password?: string;
  ready(): Promise<void>;
  clearAllTorrents(): Promise<void>;
  listTorrents(): Promise<Array<{ hash: string; name: string }>>;
  isStopped(infoHash: string): Promise<boolean>;
}

interface Scenario {
  key: string;
  driver: DriverLike;
  /** Host dir bind-mounted as the client's /downloads. */
  slug: string;
  category: string;
  addSeeding(): Promise<{ infoHash: string; contentPath: string }>;
}

const scenarios: Scenario[] = [
  {
    key: 'qBittorrent', driver: qbit, slug: 'qbittorrent', category: 'qb-stop',
    async addSeeding() {
      const dir = join(HOST_DOWNLOADS, 'qbittorrent');
      mkdirShared(dir);
      const fx = buildFolderTorrent(dir, 'stop-rule-qbittorrent', 32_768, ANNOUNCE_HOST);
      await qbit.addSeedingTorrent({ metainfo: fx.metainfo, savePath: CLIENT_DOWNLOADS, category: 'qb-stop', infoHash: fx.infoHash });
      return fx;
    },
  },
  // Transmission has no categories.
  // Cleanuparr reads the last segment of the download dir as one.
  {
    key: 'Transmission', driver: transmission, slug: 'transmission', category: 't-stop',
    async addSeeding() {
      const dir = join(HOST_DOWNLOADS, 'transmission', 't-stop');
      mkdirShared(dir);
      const fx = buildFolderTorrent(dir, 'stop-rule-transmission', 32_768, ANNOUNCE_HOST);
      await transmission.addSeedingTorrent({ metainfo: fx.metainfo, savePath: `${CLIENT_DOWNLOADS}/t-stop`, category: 't-stop', infoHash: fx.infoHash });
      return fx;
    },
  },
  {
    key: 'Deluge', driver: deluge, slug: 'deluge', category: 'dl-stop',
    async addSeeding() {
      const dir = join(HOST_DOWNLOADS, 'deluge');
      mkdirShared(dir);
      const fx = buildFolderTorrent(dir, 'stop-rule-deluge', 32_768, ANNOUNCE_HOST);
      await deluge.addSeedingTorrent({ metainfo: fx.metainfo, savePath: CLIENT_DOWNLOADS, category: 'dl-stop', name: fx.name, infoHash: fx.infoHash });
      return fx;
    },
  },
  // µTorrent is bridge-networked.
  // It reaches opentracker via host.docker.internal.
  {
    key: 'uTorrent', driver: utorrent, slug: 'utorrent', category: 'ut-stop',
    async addSeeding() {
      const dir = join(HOST_DOWNLOADS, 'utorrent');
      mkdirShared(dir);
      const fx = buildFolderTorrent(dir, 'stop-rule-utorrent', 32_768, ANNOUNCE_BRIDGE);
      await utorrent.addSeedingTorrent({ metainfo: fx.metainfo, savePath: CLIENT_DOWNLOADS, category: 'ut-stop', name: fx.name, infoHash: fx.infoHash });
      return fx;
    },
  },
];

async function isPresent(driver: DriverLike, infoHash: string): Promise<boolean> {
  const want = infoHash.toLowerCase();
  return (await driver.listTorrents()).some((t) => t.hash.toLowerCase() === want);
}

async function waitForRegistered(driver: DriverLike, infoHash: string, timeoutMs = 20_000): Promise<void> {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    if (await isPresent(driver, infoHash)) return;
    await sleep(500);
  }
  throw new Error(`torrent ${infoHash} never registered with ${driver.typeName}`);
}

async function countStoppedEvents(api: CleanuparrApi, infoHash: string): Promise<number> {
  const res = await api.events.list({ eventType: 'DownloadStopped', page: 1, pageSize: 500 });
  expect(res.status, 'events query failed').toBe(200);
  const body: { items?: Array<{ itemHash?: string }> } = await res.json();
  const want = infoHash.toLowerCase();
  return (body.items ?? []).filter((e) => (e.itemHash ?? '').toLowerCase() === want).length;
}

/**
 * Seeding rules with `action: Stop` against every live torrent client.
 *
 * Stop leaves the torrent and its data behind.
 * Client state is the only proof the rule ran.
 *
 * A stopped torrent still matches the rule.
 * Without the skip-already-stopped guard the second cycle stops it again.
 * That second stop emits a second DownloadStopped event.
 * Client state reads the same either way, so only the event count shows it.
 */
test.describe.serial('Seeding rule stop action', () => {
  let token: string;
  let api: CleanuparrApi;
  const torrents = new Map<string, { infoHash: string; contentPath: string }>();
  const clientIds: string[] = [];

  const active = () => scenarios.filter((s) => torrents.has(s.key));

  test.beforeAll(async () => {
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
  });

  test.afterAll(async () => {
    for (const s of active()) {
      await s.driver.clearAllTorrents().catch(() => {});
    }
    for (const id of clientIds) {
      await deleteDownloadClient(token, id).catch(() => {});
    }
  });

  for (const s of scenarios) {
    test(`${s.key}: sets up a seeding torrent and a stop rule`, async () => {
      test.setTimeout(120_000);

      resetDirectory(join(HOST_DOWNLOADS, s.slug));
      await s.driver.ready();
      await s.driver.clearAllTorrents();

      const fixture = await s.addSeeding();
      await waitForRegistered(s.driver, fixture.infoHash);
      // Without this the stopped assertion could pass on a torrent that never started.
      await expect
        .poll(() => s.driver.isStopped(fixture.infoHash), {
          message: `${s.key}: torrent never started seeding`,
          timeout: 30_000,
          intervals: [500],
        })
        .toBe(false);
      torrents.set(s.key, fixture);

      const createRes = await createDownloadClient(token, {
        enabled: true,
        name: `${s.key} stop rule e2e`,
        typeName: s.driver.typeName,
        type: 'Torrent',
        host: s.driver.cleanuparrHost,
        username: s.driver.username ?? '',
        password: s.driver.password ?? '',
      });
      expect(createRes.status).toBeGreaterThanOrEqual(200);
      expect(createRes.status).toBeLessThan(300);
      const clientId = (await createRes.json()).id;
      clientIds.push(clientId);

      // A maxSeedTime of 0 matches as soon as the torrent seeds.
      const ruleRes = await createSeedingRule(token, clientId, {
        name: `${s.key} stop on max seed time`,
        categories: [s.category],
        trackerPatterns: [],
        tagsAny: [],
        tagsAll: [],
        privacyType: 'Both',
        maxRatio: -1,
        minSeedTime: 0,
        maxSeedTime: 0,
        minSeeders: 0,
        maxInactiveDays: -1,
        action: 'Stop',
        deleteSourceFiles: false,
      });
      expect(ruleRes.status).toBe(201);
    });
  }

  test('stops matching torrents and leaves them and their files in place', async () => {
    test.setTimeout(180_000);

    const trig = await triggerJob(token, 'DownloadCleaner');
    expect(trig.ok, `triggerJob: ${trig.status}`).toBe(true);
    await sleep(10_000); // The job waits 10 s for the Arr queue sync.

    for (const s of active()) {
      const { infoHash, contentPath } = torrents.get(s.key)!;
      await expect
        .poll(() => s.driver.isStopped(infoHash), {
          message: `${s.key}: torrent was not stopped`,
          timeout: 60_000,
          intervals: [1_000],
        })
        .toBe(true);
      expect(await isPresent(s.driver, infoHash), `${s.key}: torrent was removed from the client`).toBe(true);
      expect(existsSync(contentPath), `${s.key}: torrent data was deleted: ${contentPath}`).toBe(true);
    }
  });

  test('emits a single stopped event even when the job runs again', async () => {
    test.setTimeout(180_000);

    for (const s of active()) {
      await expect
        .poll(() => countStoppedEvents(api, torrents.get(s.key)!.infoHash), {
          message: `${s.key}: the first cycle did not record exactly one DownloadStopped event`,
          timeout: 30_000,
          intervals: [1_000],
        })
        .toBe(1);
    }

    const trig = await triggerJob(token, 'DownloadCleaner');
    expect(trig.ok, `triggerJob: ${trig.status}`).toBe(true);
    // A duplicate proves itself by not showing up.
    // This rides out the whole cycle instead of polling.
    await sleep(20_000);

    for (const s of active()) {
      const { infoHash } = torrents.get(s.key)!;
      expect(await countStoppedEvents(api, infoHash), `${s.key}: the torrent was stopped twice`).toBe(1);
      expect(await s.driver.isStopped(infoHash), `${s.key}: torrent no longer stopped`).toBe(true);
    }
  });
});
