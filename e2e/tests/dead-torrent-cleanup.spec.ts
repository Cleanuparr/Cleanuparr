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
const MAX_STRIKES = 3;
const DEAD_ANNOUNCE = 'http://tracker.invalid/announce';
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

const qbit = new QBittorrentDriver();
const transmission = new TransmissionDriver();
const deluge = new DelugeDriver();
const utorrent = new UTorrentDriver();

/**
 * One per distinct move path in DownloadService.ChangeTorrentCategoryAsync.
 * Two scenarios may share the same physical client (e.g. qBittorrent category
 * vs tag) — they use different source categories so they never overlap, and
 * the physical client is prepared (ready/clear/reset) only once.
 */
interface Scenario {
  key: string;
  driver: DriverLike;
  /** Host dir slug = the client's bind-mounted /downloads. */
  physicalSlug: string;
  /** Source category the dead/alive torrents live in. */
  source: string;
  useTag: boolean;
  aliveAnnounce: string;
  /**
   * Whether a torrent the client is the sole seeder of can be told apart from a
   * dead one. qBittorrent/Transmission/Deluge derive SeederCount from the tracker
   * scrape, which counts the local seed, so a reachable-tracker solo seed reports
   * >= 1 and is spared. uTorrent derives it from connectable seeders discovered in
   * the swarm (the local instance is excluded and there are no other peers), so a
   * solo self-seed reports 0 — indistinguishable from dead — and gets moved.
   */
  soloSeedSparable: boolean;
  /** Builds + adds a seeding torrent in this scenario's source category. */
  addSeeding(name: string, announce: string): Promise<string>;
  /** True once the torrent has been moved to the target category / tagged. */
  isMoved(infoHash: string): Promise<boolean>;
}

function buildTorrent(physicalSlug: string, name: string, announce: string, subdir = ''): { metainfo: Buffer; infoHash: string; name: string } {
  const dir = subdir ? join(HOST_DOWNLOADS, physicalSlug, subdir) : join(HOST_DOWNLOADS, physicalSlug);
  mkdirSync(dir, { recursive: true });
  chmodIgnoringEPERM(dir, 0o777);
  const fx = buildFolderTorrent(dir, name, 32_768, announce);
  return { metainfo: fx.metainfo, infoHash: fx.infoHash, name };
}

const scenarios: Scenario[] = [
  // qBittorrent — category mode (changes the torrent's category to the target).
  {
    key: 'qBittorrent (category)', driver: qbit, physicalSlug: 'qbittorrent', source: 'qb-cat', useTag: false, aliveAnnounce: ALIVE_ANNOUNCE_HOST, soloSeedSparable: true,
    async addSeeding(name, announce) {
      const d = buildTorrent('qbittorrent', name, announce);
      await qbit.addSeedingTorrent({ metainfo: d.metainfo, savePath: CLIENT_DOWNLOADS, category: 'qb-cat', infoHash: d.infoHash });
      return d.infoHash;
    },
    async isMoved(hash) {
      return (await qbit.getTorrentCategory(hash)) === TARGET;
    },
  },
  // qBittorrent — tag mode (adds the target as a tag, category preserved).
  {
    key: 'qBittorrent (tag)', driver: qbit, physicalSlug: 'qbittorrent', source: 'qb-tag', useTag: true, aliveAnnounce: ALIVE_ANNOUNCE_HOST, soloSeedSparable: true,
    async addSeeding(name, announce) {
      const d = buildTorrent('qbittorrent', name, announce);
      await qbit.addSeedingTorrent({ metainfo: d.metainfo, savePath: CLIENT_DOWNLOADS, category: 'qb-tag', infoHash: d.infoHash });
      return d.infoHash;
    },
    async isMoved(hash) {
      return (await qbit.getTorrentTags(hash)).map((t) => t.toLowerCase()).includes(TARGET);
    },
  },
  // Transmission — label/tag mode (adds the target as a label).
  {
    key: 'Transmission (label)', driver: transmission, physicalSlug: 'transmission', source: 't-label', useTag: true, aliveAnnounce: ALIVE_ANNOUNCE_HOST, soloSeedSparable: true,
    async addSeeding(name, announce) {
      const d = buildTorrent('transmission', name, announce, 't-label');
      await transmission.addSeedingTorrent({ metainfo: d.metainfo, savePath: `${CLIENT_DOWNLOADS}/t-label`, category: 't-label', infoHash: d.infoHash });
      return d.infoHash;
    },
    async isMoved(hash) {
      return (await transmission.getTorrentLabels(hash)).map((l) => l.toLowerCase()).includes(TARGET);
    },
  },
  // Transmission — category mode (relocates files; new dir ends with the target).
  {
    key: 'Transmission (category)', driver: transmission, physicalSlug: 'transmission', source: 't-loc', useTag: false, aliveAnnounce: ALIVE_ANNOUNCE_HOST, soloSeedSparable: true,
    async addSeeding(name, announce) {
      const d = buildTorrent('transmission', name, announce, 't-loc');
      await transmission.addSeedingTorrent({ metainfo: d.metainfo, savePath: `${CLIENT_DOWNLOADS}/t-loc`, category: 't-loc', infoHash: d.infoHash });
      return d.infoHash;
    },
    async isMoved(hash) {
      const dir = (await transmission.getTorrentDownloadDir(hash)) ?? '';
      return dir.replace(/\/$/, '').endsWith(`/${TARGET}`);
    },
  },
  // Deluge — label.
  {
    key: 'Deluge', driver: deluge, physicalSlug: 'deluge', source: 'dl-src', useTag: false, aliveAnnounce: ALIVE_ANNOUNCE_HOST, soloSeedSparable: true,
    async addSeeding(name, announce) {
      const d = buildTorrent('deluge', name, announce);
      await deluge.addSeedingTorrent({ metainfo: d.metainfo, savePath: CLIENT_DOWNLOADS, category: 'dl-src', name: d.name, infoHash: d.infoHash });
      return d.infoHash;
    },
    async isMoved(hash) {
      return (await deluge.getTorrentLabel(hash)) === TARGET;
    },
  },
  // µTorrent — label (bridge-networked → reaches opentracker via host.docker.internal).
  {
    key: 'uTorrent', driver: utorrent, physicalSlug: 'utorrent', source: 'ut-src', useTag: false, aliveAnnounce: ALIVE_ANNOUNCE_BRIDGE, soloSeedSparable: false,
    async addSeeding(name, announce) {
      const d = buildTorrent('utorrent', name, announce);
      await utorrent.addSeedingTorrent({ metainfo: d.metainfo, savePath: CLIENT_DOWNLOADS, category: 'ut-src', name: d.name, infoHash: d.infoHash });
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
 * Dead torrent cleanup e2e covering every move path: a seeding torrent whose
 * tracker is unreachable (no seeders) is moved/tagged after MAX_STRIKES runs,
 * while a torrent that has seeders (via opentracker) is left untouched.
 */
test.describe.serial('Dead torrent cleanup', () => {
  let token: string;
  const dead = new Map<string, string>();
  const alive = new Map<string, string>();
  const prepared = new Set<string>();

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
    test(`${s.key}: set up dead + alive seeding torrents`, async () => {
      test.setTimeout(120_000);

      // Prepare the physical client once (multiple scenarios may share it).
      if (!prepared.has(s.physicalSlug)) {
        resetDirectory(join(HOST_DOWNLOADS, s.physicalSlug));
        await s.driver.ready();
        await s.driver.clearAllTorrents();
        prepared.add(s.physicalSlug);
      }

      const createRes = await createDownloadClient(token, {
        enabled: true,
        name: `${s.key} dead e2e`,
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
        categories: [s.source],
      });
      expect(cfg.status).toBe(200);

      const deadHash = await s.addSeeding(`dead-${s.physicalSlug}-${s.source}`, DEAD_ANNOUNCE);
      const aliveHash = await s.addSeeding(`alive-${s.physicalSlug}-${s.source}`, s.aliveAnnounce);
      dead.set(s.key, deadHash);
      alive.set(s.key, aliveHash);

      await waitForRegistered(s.driver, deadHash);
      await waitForRegistered(s.driver, aliveHash);
      expect(await s.isMoved(deadHash)).toBe(false);
      expect(await s.isMoved(aliveHash)).toBe(false);
    });
  }

  test('moves dead torrents but leaves seeded torrents untouched', async () => {
    test.setTimeout(180_000);

    const active = () => scenarios.filter((s) => dead.has(s.key));
    const moved = new Map<string, boolean>();

    for (let run = 0; run < MAX_STRIKES + 3; run++) {
      if (active().every((s) => moved.get(s.key))) break;
      const trig = await triggerJob(token, 'DownloadCleaner');
      expect(trig.ok, `triggerJob: ${trig.status}`).toBe(true);
      await sleep(13_000); // ride out the job's ~10s Arr-sync delay + processing
      for (const s of active()) {
        if (!moved.get(s.key)) {
          moved.set(s.key, await s.isMoved(dead.get(s.key)!));
        }
      }
    }

    for (const s of active()) {
      expect(moved.get(s.key), `${s.key}: dead torrent was not moved/tagged`).toBe(true);
      if (s.soloSeedSparable) {
        expect(await s.isMoved(alive.get(s.key)!), `${s.key}: seeded torrent was wrongly moved`).toBe(false);
      }
    }
  });
});
