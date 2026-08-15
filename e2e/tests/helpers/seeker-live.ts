import { resolve } from 'node:path';
import { expect } from '@playwright/test';
import { TEST_CONFIG } from './test-config';
import { buildSingleFileTorrent } from './torrent-fixtures';
import type { CleanuparrApi } from './api';
import type { ArrType } from './api/arr';
import { LiveArr, indexerMock, liveRadarr, liveSonarr } from './live-arr';
import { QBittorrentDriver } from './torrent-clients/qbittorrent';
import { TORZNAB_MOVIE_CATEGORY, TORZNAB_TV_CATEGORY } from './mocks/torznab-stubs';

/**
 * Shared arrangement for the specs that drive the Seeker against a real arr.
 *
 * tests/live-arr runs against the shipped image and waits for the cron schedule.
 * tests/live-arr-fast runs against a patched image and triggers each run itself.
 */

/** Shortest interval the Seeker config accepts, see Constants.MinSearchIntervalMinutes. */
export const SCHEDULED_SEARCH_INTERVAL_MINUTES = 2;

/**
 * Longest interval the Seeker config accepts.
 *
 * A spec that triggers runs itself must not race a scheduled one.
 * At six hours the schedule never fires inside a test.
 */
export const MANUAL_SEARCH_INTERVAL_MINUTES = 360;

/**
 * How long a scheduled search takes to reach the indexer.
 *
 * The job runs on its cron schedule, not on the trigger, and then adds jitter.
 */
export const SCHEDULED_SEARCH_TIMEOUT = 240_000;

/** Jitter is proportional to the interval and floored at 30s. */
export const TRIGGERED_SEARCH_TIMEOUT = 90_000;

/** The monitor polls once a minute. */
export const TRANSITION_TIMEOUT = 180_000;

/**
 * Makes every release, and so every infohash, unique to this run.
 * An arr refuses to track a download id it has already removed.
 */
export const RUN_TAG = Date.now().toString(36);

export interface SeededArr {
  type: ArrType;
  arr: LiveArr;
  url: string;
  apiKey: string;
  version: number;
  searchMode: 'tvsearch' | 'movie';
  category: number;
  release: string;
  /** Id of the seeded library item, which is always 1 in a freshly restored seed. */
  itemId: number;
  /** The arr endpoint holding that item. */
  itemPath: 'series' | 'movie';
  /** Title the seed gave that item. */
  seededTitle: string;
  /** Download client category the arr grabs into, set by the seed. */
  downloadCategory: string;
}

export const SONARR: SeededArr = {
  type: 'sonarr',
  arr: liveSonarr,
  url: TEST_CONFIG.liveArr.sonarrUrl,
  apiKey: TEST_CONFIG.liveArr.sonarrApiKey,
  version: 4,
  searchMode: 'tvsearch',
  category: TORZNAB_TV_CATEGORY,
  release: `Agatha.All.Along.S01E01.1080p.WEB-DL.DDP5.1.H.264-E2E${RUN_TAG}`,
  itemId: 1,
  itemPath: 'series',
  seededTitle: TEST_CONFIG.liveArr.seededSeriesTitle,
  downloadCategory: 'tv-sonarr',
};

export const RADARR: SeededArr = {
  type: 'radarr',
  arr: liveRadarr,
  url: TEST_CONFIG.liveArr.radarrUrl,
  apiKey: TEST_CONFIG.liveArr.radarrApiKey,
  version: 6,
  searchMode: 'movie',
  category: TORZNAB_MOVIE_CATEGORY,
  release: `F1.2025.1080p.WEB-DL.DDP5.1.H.264-E2E${RUN_TAG}`,
  itemId: 1,
  itemPath: 'movie',
  seededTitle: TEST_CONFIG.liveArr.seededMovieTitle,
  downloadCategory: 'radarr',
};

export const qbittorrent = new QBittorrentDriver();

/** Outside the qBittorrent save path, so a decoy download never finds its data. */
const DECOY_DIR = resolve(__dirname, '..', '..', 'test-data', 'torznab-src');

export interface SearchEvent {
  id: string;
  itemTitle: string;
  searchStatus: string | null;
  isDryRun: boolean;
  grabbedItems: string[] | null;
  searchReason: string;
}

export async function listSearchEvents(api: CleanuparrApi, instanceId: string): Promise<SearchEvent[]> {
  const res = await api.seeker.getSearchEvents({ page: '1', pageSize: '50', instanceId });
  const body = await res.json();
  return body.items ?? body.records ?? body;
}

export async function firstSearchEvent(api: CleanuparrApi, instanceId: string): Promise<SearchEvent | undefined> {
  return (await listSearchEvents(api, instanceId))[0];
}

export interface SeekerOverrides {
  instance?: Record<string, unknown>;
  config?: Record<string, unknown>;
}

/** Tracks what each spec created so the shared teardown can undo it. */
export const createdInstances: Array<{ type: ArrType; id: string }> = [];

let savedConfig: Record<string, unknown> | undefined;

/** Registers the instance with Cleanuparr and turns the Seeker on for it. */
export async function arrangeInstance(
  api: CleanuparrApi,
  target: SeededArr,
  overrides: SeekerOverrides = {},
): Promise<string> {
  const instance = await (
    await api.arr.createInstance(target.type, {
      name: `E2E live ${target.type}`,
      url: target.url,
      apiKey: target.apiKey,
      version: target.version,
    })
  ).json();

  createdInstances.push({ type: target.type, id: instance.id });

  const config = await (await api.seeker.getConfig()).json();
  // searchInterval is deliberately absent.
  // Restoring a short one re-arms the cron between tests, which then races them.
  savedConfig ??= {
    searchEnabled: config.searchEnabled,
    proactiveSearchEnabled: config.proactiveSearchEnabled,
    selectionStrategy: config.selectionStrategy,
    useRoundRobin: config.useRoundRobin,
    postReleaseGraceHours: config.postReleaseGraceHours,
  };

  const instances = config.instances.map((i: { arrInstanceId: string }) =>
    i.arrInstanceId === instance.id
      ? { ...i, enabled: true, monitoredOnly: true, minCycleTimeDays: 0, ...overrides.instance }
      : i,
  );

  const updated = await api.seeker.updateConfig({
    ...config,
    instances,
    searchEnabled: true,
    proactiveSearchEnabled: true,
    searchInterval: MANUAL_SEARCH_INTERVAL_MINUTES,
    ...overrides.config,
  });

  // A rejected update leaves the Seeker on its old settings and the test just times out.
  if (!updated.ok) {
    throw new Error(`Seeker config update failed: ${await updated.text()}`);
  }

  // An override the API drops would make a test pass for the wrong reason.
  const stored = await (await api.seeker.getConfig()).json();
  const storedInstance = stored.instances.find(
    (i: { arrInstanceId: string }) => i.arrInstanceId === instance.id,
  );

  expect(storedInstance, `the Seeker config lost instance ${instance.id}`).toBeTruthy();
  for (const [key, value] of Object.entries(overrides.instance ?? {})) {
    expect(storedInstance[key], `instance override ${key} did not persist`).toEqual(value);
  }
  for (const [key, value] of Object.entries(overrides.config ?? {})) {
    expect(stored[key], `config override ${key} did not persist`).toEqual(value);
  }

  return instance.id;
}

/** Removes the instances and restores the settings the specs changed. */
export async function teardownInstances(api: CleanuparrApi): Promise<void> {
  for (const instance of createdInstances.splice(0)) {
    await api.arr.deleteInstance(instance.type, instance.id);
  }

  if (savedConfig) {
    const config = await (await api.seeker.getConfig()).json();
    await api.seeker.updateConfig({ ...config, ...savedConfig });
  }
}

/**
 * Every library item, as the arr returns it.
 *
 * Nothing else puts an arr back: the seed is copied in before the containers start.
 * A spec takes this in its setup and restores it in its teardown.
 */
export async function snapshotLibrary(target: SeededArr): Promise<Array<Record<string, unknown>>> {
  return target.arr.get<Array<Record<string, unknown>>>(`/api/v3/${target.itemPath}`);
}

/**
 * Writes every item back, because a no-op PUT costs less than a diff.
 *
 * A PUT cannot restore an item a spec added or removed.
 * Adding one back would send the arr to TMDB or Skyhook, which the seed avoids.
 * So this fails loudly instead of leaking the change into the next spec.
 */
export async function restoreLibrary(target: SeededArr, snapshot: Array<Record<string, unknown>>): Promise<void> {
  for (const item of snapshot) {
    await target.arr.put(`/api/v3/${target.itemPath}/${item.id}`, item);
  }

  const restored = await snapshotLibrary(target);
  const expected = snapshot.map((item) => item.id).sort();
  const actual = restored.map((item) => item.id).sort();

  expect(actual, `the ${target.type} library gained or lost items, which a PUT cannot restore`).toEqual(expected);
}

/**
 * Waits for the grabbed torrent to reach the arr's queue.
 *
 * The arr only tracks a grab on its own one-minute refresh, so this drives it.
 * The Cleanuparr monitor reads that queue the moment the command completes.
 */
export async function waitForArrQueue(arr: LiveArr, downloadId: string, timeoutMs: number): Promise<void> {
  const start = Date.now();

  while (Date.now() - start < timeoutMs) {
    const records = await arr.queue();
    if (records.some((record) => record.downloadId === downloadId)) {
      return;
    }

    await arr.refreshMonitoredDownloads();
    await new Promise((resolve) => setTimeout(resolve, 1_000));
  }

  throw new Error(`Download ${downloadId} never reached the queue on ${arr.url}`);
}

/**
 * Empties both arr queues and the download client.
 *
 * The Seeker skips an item that is already downloading.
 * The client is purged first, or the arr re-adds the torrent on its next refresh.
 */
export async function resetDownloads(): Promise<void> {
  await qbittorrent.clearAllTorrents();
  await Promise.all([liveSonarr.clearQueue(), liveRadarr.clearQueue()]);
  await Promise.all([waitForEmptyQueue(liveSonarr), waitForEmptyQueue(liveRadarr)]);
}

async function waitForEmptyQueue(arr: LiveArr, timeoutMs = 120_000): Promise<void> {
  const start = Date.now();

  while (Date.now() - start < timeoutMs) {
    await arr.refreshMonitoredDownloads();
    await new Promise((resolve) => setTimeout(resolve, 1_000));

    if ((await arr.queue()).length === 0) {
      return;
    }

    await arr.clearQueue();
  }

  throw new Error(`Queue on ${arr.url} did not drain`);
}

/** Restores the indexer's file-based mappings and clears every download. */
export async function resetLiveArrState(): Promise<void> {
  await indexerMock.resetAll();
  await qbittorrent.ready();
  await resetDownloads();
}

/**
 * Fills an arr's queue with downloads that belong to nothing in its library.
 *
 * The arr lists them because Cleanuparr asks for unknown items too.
 * They stall with everything left to download, so they count as active.
 */
export async function addDecoyDownloads(target: SeededArr, count: number): Promise<void> {
  for (let i = 0; i < count; i++) {
    const torrent = buildSingleFileTorrent(
      DECOY_DIR,
      `E2E.Decoy.${RUN_TAG}.${i}`,
      16_384,
      'http://127.0.0.1:6969/announce',
    );

    await qbittorrent.addStalledTorrent({
      metainfo: torrent.metainfo,
      savePath: '/downloads',
      category: target.downloadCategory,
    });
  }

  // totalRecords, not the record count: a page holds at most 200.
  await expect
    .poll(async () => {
      await target.arr.refreshMonitoredDownloads();
      return (await target.arr.queuePage(1)).totalRecords;
    }, { timeout: 90_000 })
    .toBeGreaterThanOrEqual(count);

  // Cleanuparr only counts a record with bytes left, so a complete decoy is useless.
  const page = await target.arr.queuePage(1);
  const active = (page.records ?? []).filter((record) => record.sizeleft > 0);
  expect(active.length, 'the decoy downloads should still have bytes left').toBeGreaterThan(0);
}

/**
 * Runs the Seeker and asserts it searched nothing.
 *
 * With the jitter patch a run reaches its search within seconds.
 */
export async function expectNoSearch(api: CleanuparrApi, instanceId: string, waitMs = 20_000): Promise<void> {
  await triggerSeeker(api);
  await new Promise((resolve) => setTimeout(resolve, waitMs));

  expect(await listSearchEvents(api, instanceId)).toHaveLength(0);
}

/**
 * Runs the Seeker now instead of waiting for its schedule.
 *
 * The shipped build rejects this, so a failure means the image is not patched.
 * See e2e/patches and `make up-arr-fast`.
 */
export async function triggerSeeker(api: CleanuparrApi): Promise<void> {
  const res = await api.jobs.trigger('Seeker');

  expect(
    res.ok,
    `Triggering the Seeker failed with ${res.status}. Did you start the stack with 'make up-arr-fast'?`,
  ).toBe(true);
}
