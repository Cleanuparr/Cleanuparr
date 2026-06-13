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
  updateDeadTorrentConfig,
  triggerJob,
} from './helpers/app-api';
import { QBittorrentDriver } from './helpers/torrent-clients/qbittorrent';
import { TransmissionDriver } from './helpers/torrent-clients/transmission';
import { DelugeDriver } from './helpers/torrent-clients/deluge';
import { UTorrentDriver } from './helpers/torrent-clients/utorrent';
import { buildFolderTorrent, chmodIgnoringEPERM, resetDirectory } from './helpers/torrent-fixtures';

const HOST_DOWNLOADS = resolve(__dirname, '..', 'test-data', 'downloads');
const CLIENT_DOWNLOADS = '/downloads';
const TARGET = 'cleanuparr-dead';
const SOURCE = 'dead-src';
const MAX_STRIKES = 3;

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
}

/**
 * Per-client scenario. The dead torrent is built with the default unreachable
 * tracker (`tracker.invalid`), so every client reports it as having no seeders
 * → dead. Each client's "category" model and move read-back differ, so each
 * scenario provides its own `addSeeding` and `isMoved`.
 */
interface Scenario {
  driver: DriverLike;
  slug: string;
  useTag: boolean;
  /** Builds + adds the dead torrent in the source category; returns its infohash. */
  addSeeding(): Promise<string>;
  /** True once the torrent has been moved to the target category / tagged. */
  isMoved(infoHash: string): Promise<boolean>;
}

const qbit = new QBittorrentDriver();
const transmission = new TransmissionDriver();
const deluge = new DelugeDriver();
const utorrent = new UTorrentDriver();

function buildDead(slug: string, subdir = ''): { metainfo: Buffer; infoHash: string; name: string } {
  const dir = subdir ? join(HOST_DOWNLOADS, slug, subdir) : join(HOST_DOWNLOADS, slug);
  resetDirectory(dir);
  chmodIgnoringEPERM(dir, 0o777);
  const name = `dead-${slug}`;
  const fx = buildFolderTorrent(dir, name);
  return { metainfo: fx.metainfo, infoHash: fx.infoHash, name };
}

const scenarios: Scenario[] = [
  {
    driver: qbit,
    slug: 'qbittorrent',
    useTag: false,
    async addSeeding() {
      const d = buildDead('qbittorrent');
      await qbit.addSeedingTorrent({ metainfo: d.metainfo, savePath: CLIENT_DOWNLOADS, category: SOURCE, infoHash: d.infoHash });
      return d.infoHash;
    },
    async isMoved(hash) {
      return (await qbit.getTorrentCategory(hash)) === TARGET;
    },
  },
  {
    driver: transmission,
    slug: 'transmission',
    useTag: true, // Transmission "category" is path-based; use a label instead
    async addSeeding() {
      // Source category is the last path segment, so save under /downloads/<SOURCE>.
      const d = buildDead('transmission', SOURCE);
      await transmission.addSeedingTorrent({ metainfo: d.metainfo, savePath: `${CLIENT_DOWNLOADS}/${SOURCE}`, category: SOURCE, infoHash: d.infoHash });
      return d.infoHash;
    },
    async isMoved(hash) {
      return (await transmission.getTorrentLabels(hash)).map((l) => l.toLowerCase()).includes(TARGET);
    },
  },
  {
    driver: deluge,
    slug: 'deluge',
    useTag: false,
    async addSeeding() {
      const d = buildDead('deluge');
      await deluge.addSeedingTorrent({ metainfo: d.metainfo, savePath: CLIENT_DOWNLOADS, category: SOURCE, name: d.name, infoHash: d.infoHash });
      return d.infoHash;
    },
    async isMoved(hash) {
      return (await deluge.getTorrentLabel(hash)) === TARGET;
    },
  },
  {
    driver: utorrent,
    slug: 'utorrent',
    useTag: false,
    async addSeeding() {
      const d = buildDead('utorrent');
      await utorrent.addSeedingTorrent({ metainfo: d.metainfo, savePath: CLIENT_DOWNLOADS, category: SOURCE, name: d.name, infoHash: d.infoHash });
      return d.infoHash;
    },
    async isMoved(hash) {
      return (await utorrent.getTorrentLabel(hash)) === TARGET;
    },
  },
];

/**
 * Dead torrent cleanup e2e: a seeding torrent whose tracker is unreachable
 * (no seeders) is moved to the target category / tagged after MAX_STRIKES
 * consecutive Download Cleaner runs, across all four supported clients.
 */
test.describe.serial('Dead torrent cleanup', () => {
  let token: string;
  const deadHashes = new Map<string, string>();

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
    mkdirSync(HOST_DOWNLOADS, { recursive: true });
  });

  for (const s of scenarios) {
    test(`${s.driver.typeName}: set up a dead seeding torrent`, async () => {
      test.setTimeout(120_000);
      await s.driver.ready();
      await s.driver.clearAllTorrents();

      const createRes = await createDownloadClient(token, {
        enabled: true,
        name: `${s.driver.typeName} dead e2e`,
        typeName: s.driver.typeName,
        type: 'Torrent',
        host: s.driver.cleanuparrHost,
        username: s.driver.username ?? '',
        password: s.driver.password ?? '',
      });
      expect(createRes.status).toBeGreaterThanOrEqual(200);
      expect(createRes.status).toBeLessThan(300);
      const clientId = (await createRes.json()).id;

      const cfg = await updateDeadTorrentConfig(token, clientId, {
        enabled: true,
        targetCategory: TARGET,
        useTag: s.useTag,
        maxStrikes: MAX_STRIKES,
        categories: [SOURCE],
      });
      expect(cfg.status).toBe(200);

      const hash = await s.addSeeding();
      deadHashes.set(s.driver.typeName, hash);

      // Wait for the client to register the torrent.
      const want = hash.toLowerCase();
      const start = Date.now();
      let seen = false;
      while (Date.now() - start < 20_000 && !seen) {
        seen = (await s.driver.listTorrents()).some((t) => t.hash.toLowerCase() === want);
        if (!seen) await sleep(500);
      }
      expect(seen, `torrent ${hash} never registered with ${s.driver.typeName}`).toBe(true);
      expect(await s.isMoved(hash)).toBe(false);
    });
  }

  test('moves all dead torrents after reaching the strike limit', async () => {
    test.setTimeout(180_000);

    const pending = () => scenarios.filter((s) => deadHashes.has(s.driver.typeName));
    const movedState = new Map<string, boolean>();

    for (let run = 0; run < MAX_STRIKES + 3; run++) {
      if (pending().every((s) => movedState.get(s.driver.typeName))) break;
      const trig = await triggerJob(token, 'DownloadCleaner');
      expect(trig.ok, `triggerJob: ${trig.status}`).toBe(true);
      await sleep(13_000); // ride out the job's ~10s Arr-sync delay + processing
      for (const s of pending()) {
        if (!movedState.get(s.driver.typeName)) {
          movedState.set(s.driver.typeName, await s.isMoved(deadHashes.get(s.driver.typeName)!));
        }
      }
    }

    for (const s of pending()) {
      expect(movedState.get(s.driver.typeName), `${s.driver.typeName}: dead torrent was not moved/tagged`).toBe(true);
    }
  });
});
