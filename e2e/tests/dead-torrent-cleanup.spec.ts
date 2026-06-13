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
// Unreachable tracker → client reports no seeders → dead.
const DEAD_ANNOUNCE = 'http://tracker.invalid/announce';
// opentracker reachable from host-networked clients and (via host gateway) from bridged ones.
const ALIVE_ANNOUNCE_HOST = 'http://127.0.0.1:6969/announce';
const ALIVE_ANNOUNCE_BRIDGE = 'http://host.docker.internal:6969/announce';

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
 * Per-client scenario. Two torrents are added to the same source category:
 *   - a DEAD torrent (unreachable tracker → no seeders) → must be moved/tagged
 *   - an ALIVE torrent (announces to opentracker, seeded by the client → ≥1
 *     seeder) → must be left untouched (and its strikes reset).
 * Each client's "category" model and read-back differ, so each scenario
 * provides its own `addSeeding` and `isMoved`.
 */
interface Scenario {
  driver: DriverLike;
  slug: string;
  useTag: boolean;
  aliveAnnounce: string;
  /** Builds + adds a seeding torrent in the source category; returns its infohash. */
  addSeeding(name: string, announce: string): Promise<string>;
  /** True once the torrent has been moved to the target category / tagged. */
  isMoved(infoHash: string): Promise<boolean>;
}

const qbit = new QBittorrentDriver();
const transmission = new TransmissionDriver();
const deluge = new DelugeDriver();
const utorrent = new UTorrentDriver();

function buildTorrent(slug: string, name: string, announce: string, subdir = ''): { metainfo: Buffer; infoHash: string; name: string } {
  const dir = subdir ? join(HOST_DOWNLOADS, slug, subdir) : join(HOST_DOWNLOADS, slug);
  mkdirSync(dir, { recursive: true });
  chmodIgnoringEPERM(dir, 0o777);
  const fx = buildFolderTorrent(dir, name, 32_768, announce);
  return { metainfo: fx.metainfo, infoHash: fx.infoHash, name };
}

const scenarios: Scenario[] = [
  {
    driver: qbit,
    slug: 'qbittorrent',
    useTag: false,
    aliveAnnounce: ALIVE_ANNOUNCE_HOST,
    async addSeeding(name, announce) {
      const d = buildTorrent('qbittorrent', name, announce);
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
    useTag: true, // Transmission "category" is path-based; move via a label instead
    aliveAnnounce: ALIVE_ANNOUNCE_HOST,
    async addSeeding(name, announce) {
      // Source category is the last path segment, so save under /downloads/<SOURCE>.
      const d = buildTorrent('transmission', name, announce, SOURCE);
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
    aliveAnnounce: ALIVE_ANNOUNCE_HOST,
    async addSeeding(name, announce) {
      const d = buildTorrent('deluge', name, announce);
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
    aliveAnnounce: ALIVE_ANNOUNCE_BRIDGE,
    async addSeeding(name, announce) {
      const d = buildTorrent('utorrent', name, announce);
      await utorrent.addSeedingTorrent({ metainfo: d.metainfo, savePath: CLIENT_DOWNLOADS, category: SOURCE, name: d.name, infoHash: d.infoHash });
      return d.infoHash;
    },
    async isMoved(hash) {
      return (await utorrent.getTorrentLabel(hash)) === TARGET;
    },
  },
];

async function waitForRegistered(driver: DriverLike, infoHash: string, timeoutMs = 20_000): Promise<void> {
  const want = infoHash.toLowerCase();
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    if ((await driver.listTorrents()).some((t) => t.hash.toLowerCase() === want)) return;
    await sleep(500);
  }
  throw new Error(`torrent ${infoHash} never registered with ${driver.typeName}`);
}

/**
 * Dead torrent cleanup e2e across all four supported clients: a seeding torrent
 * whose tracker is unreachable (no seeders) is moved/tagged after MAX_STRIKES
 * runs, while a torrent that has seeders (via opentracker) is left untouched.
 */
test.describe.serial('Dead torrent cleanup', () => {
  let token: string;
  const dead = new Map<string, string>();
  const alive = new Map<string, string>();

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
    test(`${s.driver.typeName}: set up dead + alive seeding torrents`, async () => {
      test.setTimeout(120_000);
      resetDirectory(join(HOST_DOWNLOADS, s.slug));
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

      const deadHash = await s.addSeeding(`dead-${s.slug}`, DEAD_ANNOUNCE);
      const aliveHash = await s.addSeeding(`alive-${s.slug}`, s.aliveAnnounce);
      dead.set(s.driver.typeName, deadHash);
      alive.set(s.driver.typeName, aliveHash);

      await waitForRegistered(s.driver, deadHash);
      await waitForRegistered(s.driver, aliveHash);
      expect(await s.isMoved(deadHash)).toBe(false);
      expect(await s.isMoved(aliveHash)).toBe(false);
    });
  }

  test('moves dead torrents but leaves seeded torrents untouched', async () => {
    test.setTimeout(180_000);

    const active = () => scenarios.filter((s) => dead.has(s.driver.typeName));
    const moved = new Map<string, boolean>();

    for (let run = 0; run < MAX_STRIKES + 3; run++) {
      if (active().every((s) => moved.get(s.driver.typeName))) break;
      const trig = await triggerJob(token, 'DownloadCleaner');
      expect(trig.ok, `triggerJob: ${trig.status}`).toBe(true);
      await sleep(13_000); // ride out the job's ~10s Arr-sync delay + processing
      for (const s of active()) {
        if (!moved.get(s.driver.typeName)) {
          moved.set(s.driver.typeName, await s.isMoved(dead.get(s.driver.typeName)!));
        }
      }
    }

    for (const s of active()) {
      expect(moved.get(s.driver.typeName), `${s.driver.typeName}: dead torrent was not moved/tagged`).toBe(true);
      // The seeded torrent must NOT have been moved/tagged.
      expect(await s.isMoved(alive.get(s.driver.typeName)!), `${s.driver.typeName}: seeded torrent was wrongly moved`).toBe(false);
    }
  });
});
