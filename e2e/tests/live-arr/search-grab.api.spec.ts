import { test, expect, TEST_CONFIG } from '../fixtures/base';
import type { CleanuparrApi } from '../helpers/api';
import type { ArrType } from '../helpers/api/arr';
import { LiveArr, indexerMock, liveRadarr, liveSonarr } from '../helpers/live-arr';
import { QBittorrentDriver } from '../helpers/torrent-clients/qbittorrent';
import {
  TORZNAB_MOVIE_CATEGORY,
  TORZNAB_TV_CATEGORY,
  grabbableRelease,
  torznabSearchStub,
} from '../helpers/mocks/torznab-stubs';

/**
 * The Seeker against real Sonarr and Radarr containers.
 *
 * Only the indexer is faked.
 * The command states no real arr produces are covered by tests/seeker.
 */

/** Shortest interval the Seeker config accepts, see Constants.MinSearchIntervalMinutes. */
const SEARCH_INTERVAL_MINUTES = 2;

/**
 * How long a triggered search takes to reach the indexer.
 *
 * The job runs on its cron schedule, not on the trigger, and then adds jitter.
 */
const SEARCH_TIMEOUT = 240_000;

/** The monitor polls once a minute and the Seeker adds up to 30s of jitter. */
const TRANSITION_TIMEOUT = 180_000;

/** Every arr command state Cleanuparr knows how to map. */
const KNOWN_COMMAND_STATES = ['queued', 'started', 'completed', 'failed', 'aborted', 'cancelled', 'orphaned'];

interface SearchEvent {
  id: string;
  itemTitle: string;
  searchStatus: string | null;
  grabbedItems: string[] | null;
}

/**
 * Makes every release, and so every infohash, unique to this run.
 * An arr refuses to track a download id it has already removed.
 */
const RUN_TAG = Date.now().toString(36);

interface SeededArr {
  type: ArrType;
  arr: LiveArr;
  url: string;
  apiKey: string;
  version: number;
  searchMode: 'tvsearch' | 'movie';
  category: number;
  release: string;
}

const SONARR: SeededArr = {
  type: 'sonarr',
  arr: liveSonarr,
  url: TEST_CONFIG.liveArr.sonarrUrl,
  apiKey: TEST_CONFIG.liveArr.sonarrApiKey,
  version: 4,
  searchMode: 'tvsearch',
  category: TORZNAB_TV_CATEGORY,
  release: `Agatha.All.Along.S01E01.1080p.WEB-DL.DDP5.1.H.264-E2E${RUN_TAG}`,
};

const RADARR: SeededArr = {
  type: 'radarr',
  arr: liveRadarr,
  url: TEST_CONFIG.liveArr.radarrUrl,
  apiKey: TEST_CONFIG.liveArr.radarrApiKey,
  version: 6,
  searchMode: 'movie',
  category: TORZNAB_MOVIE_CATEGORY,
  release: `F1.2025.1080p.WEB-DL.DDP5.1.H.264-E2E${RUN_TAG}`,
};

const qbittorrent = new QBittorrentDriver();
const created: Array<{ type: ArrType; id: string }> = [];
let savedSearchSettings: Record<string, unknown> | undefined;

async function listSearchEvents(api: CleanuparrApi, instanceId: string): Promise<SearchEvent[]> {
  const res = await api.seeker.getSearchEvents({ page: '1', pageSize: '50', instanceId });
  const body = await res.json();
  return body.items ?? body.records ?? body;
}

async function firstSearchEvent(api: CleanuparrApi, instanceId: string): Promise<SearchEvent | undefined> {
  return (await listSearchEvents(api, instanceId))[0];
}

/** Registers the instance with Cleanuparr and turns the Seeker on for it. */
async function arrangeInstance(api: CleanuparrApi, target: SeededArr): Promise<string> {
  const instance = await (
    await api.arr.createInstance(target.type, {
      name: `E2E live ${target.type}`,
      url: target.url,
      apiKey: target.apiKey,
      version: target.version,
    })
  ).json();

  created.push({ type: target.type, id: instance.id });

  const config = await (await api.seeker.getConfig()).json();
  savedSearchSettings ??= {
    searchEnabled: config.searchEnabled,
    proactiveSearchEnabled: config.proactiveSearchEnabled,
    searchInterval: config.searchInterval,
  };

  const instances = config.instances.map((i: { arrInstanceId: string }) =>
    i.arrInstanceId === instance.id ? { ...i, enabled: true, monitoredOnly: true } : i,
  );

  const updated = await api.seeker.updateConfig({
    ...config,
    instances,
    searchEnabled: true,
    proactiveSearchEnabled: true,
    searchInterval: SEARCH_INTERVAL_MINUTES,
  });

  // A rejected update leaves the Seeker on its old schedule and the test just times out.
  if (!updated.ok) {
    throw new Error(`Seeker config update failed: ${await updated.text()}`);
  }

  return instance.id;
}

/**
 * Waits for the grabbed torrent to reach the arr's queue.
 *
 * The arr only tracks a grab on its own one-minute refresh, so this drives it.
 * The Cleanuparr monitor reads that queue the moment the command completes.
 */
async function waitForArrQueue(arr: LiveArr, downloadId: string, timeoutMs = SEARCH_TIMEOUT): Promise<void> {
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
async function resetDownloads(): Promise<void> {
  await qbittorrent.clearAllTorrents();
  await Promise.all([liveSonarr.clearQueue(), liveRadarr.clearQueue()]);
  await Promise.all([waitForEmptyQueue(liveSonarr), waitForEmptyQueue(liveRadarr)]);
}

async function waitForEmptyQueue(arr: LiveArr, timeoutMs = 60_000): Promise<void> {
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

test.describe('Seeker against a live arr', () => {
  test.beforeEach(async () => {
    // Drops the previous test's stubs and restores the file-based mappings.
    await indexerMock.resetAll();
    await qbittorrent.ready();
    await resetDownloads();
  });

  test.afterEach(async ({ api }) => {
    for (const instance of created.splice(0)) {
      await api.arr.deleteInstance(instance.type, instance.id);
    }

    if (savedSearchSettings) {
      const config = await (await api.seeker.getConfig()).json();
      await api.seeker.updateConfig({ ...config, ...savedSearchSettings });
    }

    await resetDownloads();
  });

  for (const target of [SONARR, RADARR]) {
    test(`records the real ${target.type} grab on the search event`, async ({ api }) => {
      test.setTimeout(SEARCH_TIMEOUT + TRANSITION_TIMEOUT + 120_000);

      const release = grabbableRelease(target.searchMode, target.release, target.category);
      await indexerMock.stubMany(release.mappings);

      const instanceId = await arrangeInstance(api, target);
      await api.jobs.trigger('Seeker');

      await waitForArrQueue(target.arr, release.downloadId);

      await expect
        .poll(async () => (await firstSearchEvent(api, instanceId))?.searchStatus, { timeout: TRANSITION_TIMEOUT })
        .toBe('Completed');

      const event = await firstSearchEvent(api, instanceId);
      expect(event?.grabbedItems).toContain(release.title);
    });
  }

  test('completes the search event when the indexer returns nothing', async ({ api }) => {
    test.setTimeout(SEARCH_TIMEOUT + TRANSITION_TIMEOUT + 120_000);

    await indexerMock.stub(torznabSearchStub(SONARR.searchMode, []));

    const instanceId = await arrangeInstance(api, SONARR);
    await api.jobs.trigger('Seeker');

    await expect
      .poll(async () => (await firstSearchEvent(api, instanceId))?.searchStatus, {
        timeout: SEARCH_TIMEOUT + TRANSITION_TIMEOUT,
      })
      .toBe('Completed');

    const event = await firstSearchEvent(api, instanceId);
    expect(event?.grabbedItems ?? []).toHaveLength(0);
    expect(await SONARR.arr.queue()).toHaveLength(0);
  });

  // Guards the ArrCommandState enum against a state a future arr release invents.
  for (const target of [SONARR, RADARR]) {
    test(`reports only known command states on ${target.type}`, async () => {
      await target.arr.refreshMonitoredDownloads();
      const commands = await target.arr.commands();

      expect(commands.length).toBeGreaterThan(0);
      for (const command of commands) {
        expect(KNOWN_COMMAND_STATES, `${command.name} reported '${command.status}'`).toContain(command.status);
      }
    });
  }
});
