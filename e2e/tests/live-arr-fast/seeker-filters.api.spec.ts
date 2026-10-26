import { test, expect, TEST_CONFIG } from '../fixtures/base';
import type { CleanuparrApi } from '../helpers/api';
import { indexerMock } from '../helpers/live-arr';
import {
  LIDARR,
  RADARR,
  SONARR,
  WHISPARR,
  addDecoyDownloads,
  arrangeInstance,
  arrangeQueueCleaner,
  createStallRule,
  expectNoSearch,
  firstSearchEvent,
  listSearchEvents,
  resetDownloads,
  resetLiveArrState,
  restoreLibrary,
  snapshotLibrary,
  teardownInstances,
  teardownQueueCleaner,
  triggerSeeker,
  updateInstance,
  RUN_TAG,
  TRIGGERED_SEARCH_TIMEOUT,
  type SeededArr,
} from '../helpers/seeker-live';
import { grabbableRelease, grabbableReleases, torznabSearchStub } from '../helpers/mocks/torznab-stubs';

/**
 * The per-instance filters that decide whether the Seeker searches at all.
 *
 * Each test drives a real arr, so the library state comes from real API calls.
 * The teardown puts the library back, whatever a test did to it.
 */

const SKIP_TAG = 'e2e-seeker-skip';
const INCLUDE_TAG = 'e2e-seeker-include';

/** A search event shows up within a second of the run, so this is generous. */
const SETTLE_MS = 20_000;

/** The stall rule floor is above one, so one run leaves the grab in the queue. */
const MAX_STRIKES = 3;

/** The second seeded movie, so a search grabs whichever one the cycle picks. */
const SECOND_MOVIE_RELEASE = `The.Wild.Robot.2024.1080p.WEB-DL.DDP5.1.H.264-E2E${RUN_TAG}`;

async function mutateMovie(mutate: (movie: Record<string, unknown>) => Record<string, unknown>): Promise<void> {
  const movie = await RADARR.arr.get<Record<string, unknown>>(`/api/v3/movie/${RADARR.itemId}`);
  await RADARR.arr.put(`/api/v3/movie/${RADARR.itemId}`, mutate({ ...movie }));
}

/** Pushes Radarr's isAvailable for the seeded movie past today. */
const UNAVAILABLE_DELAY_DAYS = 3650;

async function withAvailabilityDelay(days: number, run: () => Promise<void>): Promise<void> {
  const original = await RADARR.arr.get<Record<string, unknown>>('/api/v3/config/indexer');
  await RADARR.arr.put('/api/v3/config/indexer', { ...original, availabilityDelay: days });

  try {
    await run();
  } finally {
    await RADARR.arr.put('/api/v3/config/indexer', original);
  }
}

test.describe('Seeker candidate filters', () => {
  let library: Array<Record<string, unknown>> = [];

  test.beforeEach(async () => {
    await resetLiveArrState();
    library = await snapshotLibrary(RADARR);
    await indexerMock.stubMany(grabbableRelease(RADARR.searchMode, RADARR.release, RADARR.category).mappings);
  });

  test.afterEach(async ({ api }) => {
    await teardownQueueCleaner(api);
    await teardownInstances(api);
    await restoreLibrary(RADARR, library);
    await resetLiveArrState();
  });

  /**
   * Searches once, then leaves the grabbed download on one queue cleaner strike.
   *
   * A decoy cannot serve here.
   * The cleaner skips a queue item that matches nothing in the library.
   */
  async function grabAndStrike(api: CleanuparrApi, instanceId: string): Promise<void> {
    // The cycle decides which movie Seeker searches first, so both must be grabbable.
    await indexerMock.resetAll();
    await indexerMock.stubMany(
      grabbableReleases(RADARR.searchMode, [RADARR.release, SECOND_MOVIE_RELEASE], RADARR.category).mappings,
    );

    await arrangeQueueCleaner(api);
    await createStallRule(api, MAX_STRIKES);

    await triggerSeeker(api);
    await expect
      .poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS })
      .toBe(1);
    // Any active download will do, and the arr can refuse a repeat grab of one release.
    await expect
      .poll(
        async () => {
          await RADARR.arr.refreshMonitoredDownloads();
          return (await RADARR.arr.queue()).filter((record) => record.sizeleft > 0).length;
        },
        { timeout: TRIGGERED_SEARCH_TIMEOUT },
      )
      .toBeGreaterThan(0);

    // A run that lands before the torrent stalls strikes nothing, so allow a second one.
    // Both stay under the rule's 3 strikes, which keeps the download in the queue.
    for (let attempt = 0; attempt < 2; attempt++) {
      const triggered = await api.jobs.trigger('QueueCleaner');
      expect(triggered.status).toBeLessThan(300);

      if (await pollStrikeCount(api)) {
        return;
      }
    }

    throw new Error('the queue cleaner never struck the grabbed download');
  }

  /**
   * Triggers one search and waits for the arr to record its grab.
   *
   * History, not the queue: the arr can refuse to track a release an earlier test grabbed.
   */
  async function grabOnce(api: CleanuparrApi, instanceId: string, target: SeededArr): Promise<void> {
    const since = encodeURIComponent(new Date().toISOString());

    await triggerSeeker(api);
    await expect
      .poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS })
      .toBe(1);

    await expect
      .poll(
        async () => (await target.arr.get<unknown[]>(`/api/v3/history/since?date=${since}&eventType=1`)).length,
        { timeout: TRIGGERED_SEARCH_TIMEOUT },
      )
      .toBeGreaterThan(0);
  }

  /** Whether a strike landed within the window, without failing the test if none did. */
  function pollStrikeCount(api: CleanuparrApi): Promise<boolean> {
    return expect
      .poll(
        async () => ((await (await api.strikes.list({ pageSize: 200 })).json()).items ?? []).length,
        { timeout: 45_000, intervals: [2_000] },
      )
      .toBeGreaterThan(0)
      .then(
        () => true,
        () => false,
      );
  }

  test('searches the seeded movie when nothing filters it out', async ({ api }) => {
    const instanceId = await arrangeInstance(api, RADARR);
    await triggerSeeker(api);

    await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(1);

    const event = await firstSearchEvent(api, instanceId);
    expect(event?.itemTitle).toContain(RADARR.seededTitle);
  });

  test('skips an unmonitored movie when monitoredOnly is on', async ({ api }) => {
    const instanceId = await arrangeInstance(api, RADARR, { instance: { monitoredOnly: true } });

    await mutateMovie((movie) => ({ ...movie, monitored: false }));
    await expectNoSearch(api, instanceId, SETTLE_MS);
  });

  test('searches an unmonitored movie once monitoredOnly is off', async ({ api }) => {
    // The seed's other movie is unmonitored too, so it would also qualify once monitoredOnly
    // is off. Tag it out of the run so only the movie this test unmonitors can be searched.
    const skipTagId = await RADARR.arr.ensureTag(SKIP_TAG);
    const others = (await RADARR.arr.get<Array<{ id: number }>>('/api/v3/movie')).filter(
      (movie) => movie.id !== RADARR.itemId,
    );
    for (const other of others) {
      const full = await RADARR.arr.get<Record<string, unknown>>(`/api/v3/movie/${other.id}`);
      await RADARR.arr.put(`/api/v3/movie/${other.id}`, { ...full, tags: [skipTagId] });
    }

    const instanceId = await arrangeInstance(api, RADARR, {
      instance: { monitoredOnly: false, skipTags: [SKIP_TAG] },
    });

    await mutateMovie((movie) => ({ ...movie, monitored: false }));
    await triggerSeeker(api);

    await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(1);

    const event = await firstSearchEvent(api, instanceId);
    expect(event?.itemTitle).toContain(RADARR.seededTitle);
  });

  test('skips a movie carrying a skipped tag', async ({ api }) => {
    const tagId = await RADARR.arr.ensureTag(SKIP_TAG);
    const instanceId = await arrangeInstance(api, RADARR, { instance: { skipTags: [SKIP_TAG] } });

    await mutateMovie((movie) => ({ ...movie, tags: [tagId] }));
    await expectNoSearch(api, instanceId, SETTLE_MS);
  });

  test('skips a movie without an included tag', async ({ api }) => {
    const instanceId = await arrangeInstance(api, RADARR, { instance: { includeTags: [INCLUDE_TAG] } });

    await mutateMovie((movie) => ({ ...movie, tags: [] }));
    await expectNoSearch(api, instanceId, SETTLE_MS);
  });

  test('searches a movie carrying an included tag', async ({ api }) => {
    const tagId = await RADARR.arr.ensureTag(INCLUDE_TAG);
    const instanceId = await arrangeInstance(api, RADARR, { instance: { includeTags: [INCLUDE_TAG] } });

    await mutateMovie((movie) => ({ ...movie, tags: [tagId] }));
    await triggerSeeker(api);

    await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(1);

    const event = await firstSearchEvent(api, instanceId);
    expect(event?.itemTitle).toContain(RADARR.seededTitle);
  });

  test('skips a movie carrying both an included and a skipped tag', async ({ api }) => {
    const includeId = await RADARR.arr.ensureTag(INCLUDE_TAG);
    const skipId = await RADARR.arr.ensureTag(SKIP_TAG);
    const instanceId = await arrangeInstance(api, RADARR, {
      instance: { includeTags: [INCLUDE_TAG], skipTags: [SKIP_TAG] },
    });

    await mutateMovie((movie) => ({ ...movie, tags: [includeId, skipId] }));
    await expectNoSearch(api, instanceId, SETTLE_MS);
  });

  test('skips the run while the active download limit is reached', async ({ api }) => {
    test.setTimeout(180_000);

    const instanceId = await arrangeInstance(api, RADARR, { instance: { activeDownloadLimit: 1 } });
    await addDecoyDownloads(RADARR, 2);

    await expectNoSearch(api, instanceId, SETTLE_MS);
  });

  test('searches once the active download limit is out of reach', async ({ api }) => {
    test.setTimeout(180_000);

    const instanceId = await arrangeInstance(api, RADARR, { instance: { activeDownloadLimit: 0 } });
    await addDecoyDownloads(RADARR, 2);

    await triggerSeeker(api);

    await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(1);
  });

  test('searches again while the struck download is ignored', async ({ api }) => {
    test.setTimeout(420_000);

    // monitoredOnly is off so Seeker can search a second library item.
    const instanceId = await arrangeInstance(api, RADARR, {
      instance: { activeDownloadLimit: 1, ignoreStruckDownloads: true, monitoredOnly: false },
    });
    await grabAndStrike(api, instanceId);

    await triggerSeeker(api);

    await expect
      .poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS })
      .toBe(2);
  });

  test('counts the struck download while the setting is off', async ({ api }) => {
    test.setTimeout(420_000);

    const instanceId = await arrangeInstance(api, RADARR, {
      instance: { activeDownloadLimit: 1, ignoreStruckDownloads: false, monitoredOnly: false },
    });
    await grabAndStrike(api, instanceId);

    await triggerSeeker(api);
    await new Promise((resolve) => setTimeout(resolve, SETTLE_MS));

    // The struck download still fills the only slot, so the first search stands alone.
    expect(await listSearchEvents(api, instanceId)).toHaveLength(1);
  });

  test('still searches an old release when the grace period is at its longest', async ({ api }) => {
    const instanceId = await arrangeInstance(api, RADARR, { config: { postReleaseGraceHours: 72 } });
    await triggerSeeker(api);

    await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(1);
  });

  test('skips a movie Radarr does not count as available in Radarr availability mode', async ({ api }) => {
    const instanceId = await arrangeInstance(api, RADARR, { instance: { radarrReleaseGate: 'ArrAvailability' } });

    await withAvailabilityDelay(UNAVAILABLE_DELAY_DAYS, async () => {
      await expectNoSearch(api, instanceId, SETTLE_MS);

      const preview = await (await api.seeker.getPreview(instanceId)).json();
      const excluded = preview.excluded.find((e: { name: string }) => e.name.includes(RADARR.seededTitle));
      expect(excluded?.reason).toBe('NotAvailable');
    });
  });

  test('searches the same movie with release dates', async ({ api }) => {
    const instanceId = await arrangeInstance(api, RADARR, { instance: { radarrReleaseGate: 'ReleaseDate' } });

    await withAvailabilityDelay(UNAVAILABLE_DELAY_DAYS, async () => {
      await triggerSeeker(api);

      await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(1);
    });
  });

  test('searches an available movie in Radarr availability mode', async ({ api }) => {
    const instanceId = await arrangeInstance(api, RADARR, { instance: { radarrReleaseGate: 'ArrAvailability' } });
    await triggerSeeker(api);

    await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(1);
  });

  test('leaves a dry run search without a status and grabs nothing', async ({ api }) => {
    const instanceId = await arrangeInstance(api, RADARR);

    const general = await (await api.general.getConfig()).json();
    await api.general.updateConfig({ ...general, dryRun: true });

    try {
      await triggerSeeker(api);

      await expect
        .poll(async () => (await firstSearchEvent(api, instanceId))?.isDryRun, { timeout: SETTLE_MS })
        .toBe(true);

      const event = await firstSearchEvent(api, instanceId);
      expect(event?.searchStatus ?? null).toBeNull();
      expect(await RADARR.arr.queue()).toHaveLength(0);
    } finally {
      await api.general.updateConfig(general);
    }
  });

  test('searches one instance per run under round robin', async ({ api }) => {
    test.setTimeout(180_000);

    await indexerMock.stubMany(grabbableRelease(SONARR.searchMode, SONARR.release, SONARR.category).mappings);

    const radarrId = await arrangeInstance(api, RADARR, { config: { useRoundRobin: true } });
    const sonarrId = await arrangeInstance(api, SONARR, { config: { useRoundRobin: true } });

    await triggerSeeker(api);
    await new Promise((resolve) => setTimeout(resolve, SETTLE_MS));

    const afterFirst =
      (await listSearchEvents(api, radarrId)).length + (await listSearchEvents(api, sonarrId)).length;
    expect(afterFirst).toBe(1);

    await triggerSeeker(api);

    await expect
      .poll(
        async () =>
          (await listSearchEvents(api, radarrId)).length + (await listSearchEvents(api, sonarrId)).length,
        { timeout: SETTLE_MS },
      )
      .toBe(2);
  });

  test('skips a movie grabbed within the recent grab window', async ({ api }) => {
    test.setTimeout(180_000);

    const instanceId = await arrangeInstance(api, RADARR, { instance: { monitoredOnly: true } });
    await grabOnce(api, instanceId, RADARR);
    await resetDownloads();

    await updateInstance(api, instanceId, { recentGrabWindowHours: 24 });
    await triggerSeeker(api);
    await new Promise((resolve) => setTimeout(resolve, SETTLE_MS));

    // The grab stays in arr history after resetDownloads clears the queue, so it is still skipped.
    expect(await listSearchEvents(api, instanceId)).toHaveLength(1);
  });

  test('searches a grabbed movie again with the window off', async ({ api }) => {
    test.setTimeout(180_000);

    const instanceId = await arrangeInstance(api, RADARR, { instance: { monitoredOnly: true } });
    await grabOnce(api, instanceId, RADARR);
    await resetDownloads();

    await triggerSeeker(api);

    await expect
      .poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS })
      .toBe(2);
  });

  test('skips an episode grabbed within the recent grab window', async ({ api }) => {
    test.setTimeout(180_000);

    await indexerMock.stubMany(grabbableRelease(SONARR.searchMode, SONARR.release, SONARR.category).mappings);

    const instanceId = await arrangeInstance(api, SONARR, { instance: { monitoredOnly: true } });
    await grabOnce(api, instanceId, SONARR);
    await resetDownloads();

    await updateInstance(api, instanceId, { recentGrabWindowHours: 24 });
    await triggerSeeker(api);
    await new Promise((resolve) => setTimeout(resolve, SETTLE_MS));

    expect(await listSearchEvents(api, instanceId)).toHaveLength(1);
  });
});

test.describe('Seeker monitored-only filters on Sonarr', () => {
  /**
   * Every episode in the seeded season is aired and fileless, so every one of
   * them is a "missing" gap. Unmonitoring all of them (never just S01E01,
   * whose id doubles as the series id and would hide an id mix-up) removes
   * the only gap from the monitored=true list; it only reappears once
   * monitoredOnly is off and the Seeker also reads the monitored=false list.
   */

  let library: Array<Record<string, unknown>> = [];
  let episodeIds: number[] = [];

  test.beforeEach(async () => {
    await resetLiveArrState();
    library = await snapshotLibrary(SONARR);
    await indexerMock.stub(torznabSearchStub(SONARR.searchMode, []));

    const episodes = await SONARR.arr.get<Array<{ id: number }>>(`/api/v3/episode?seriesId=${SONARR.itemId}`);
    episodeIds = episodes.map((episode) => episode.id);
    await SONARR.arr.put('/api/v3/episode/monitor', { episodeIds, monitored: false });
  });

  test.afterEach(async ({ api }) => {
    await teardownInstances(api);
    await SONARR.arr.put('/api/v3/episode/monitor', { episodeIds, monitored: true });
    await restoreLibrary(SONARR, library);
    await resetLiveArrState();
  });

  test('skips the season while its only gap is unmonitored', async ({ api }) => {
    const instanceId = await arrangeInstance(api, SONARR, { instance: { monitoredOnly: true } });
    await expectNoSearch(api, instanceId, SETTLE_MS);
  });

  test('searches the season once monitoredOnly lets the unmonitored gap count', async ({ api }) => {
    // The seed's unmonitored specials series would count too.
    const skipTagId = await SONARR.arr.ensureTag(SKIP_TAG);
    for (const other of library.filter((series) => series.id !== SONARR.itemId)) {
      await SONARR.arr.put(`/api/v3/series/${other.id}`, { ...other, tags: [skipTagId] });
    }

    const instanceId = await arrangeInstance(api, SONARR, {
      instance: { monitoredOnly: false, skipTags: [SKIP_TAG], sonarrSearchMode: 'Season' },
    });
    await triggerSeeker(api);

    await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(1);

    const event = await firstSearchEvent(api, instanceId);
    expect(event?.itemTitle).toBe(`${SONARR.seededTitle} S01`);
  });
});

test.describe('Seeker candidate filters on Whisparr v3', () => {
  let library: Array<Record<string, unknown>> = [];

  async function mutateWhisparrMovie(mutate: (movie: Record<string, unknown>) => Record<string, unknown>): Promise<void> {
    const movie = await WHISPARR.arr.get<Record<string, unknown>>(`/api/v3/movie/${WHISPARR.itemId}`);
    await WHISPARR.arr.put(`/api/v3/movie/${WHISPARR.itemId}`, mutate({ ...movie }));
  }

  test.beforeEach(async () => {
    await resetLiveArrState();
    library = await snapshotLibrary(WHISPARR);
    await indexerMock.stubMany(grabbableRelease(WHISPARR.searchMode, WHISPARR.release, WHISPARR.category).mappings);
  });

  test.afterEach(async ({ api }) => {
    await teardownInstances(api);
    await restoreLibrary(WHISPARR, library);
    await resetLiveArrState();
  });

  test('searches the seeded movie when nothing filters it out', async ({ api }) => {
    const instanceId = await arrangeInstance(api, WHISPARR);
    await triggerSeeker(api);

    await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(1);

    const event = await firstSearchEvent(api, instanceId);
    expect(event?.itemTitle).toContain(WHISPARR.seededTitle);
  });

  test('skips a movie carrying a skipped tag', async ({ api }) => {
    const tagId = await WHISPARR.arr.ensureTag(SKIP_TAG);
    const instanceId = await arrangeInstance(api, WHISPARR, { instance: { skipTags: [SKIP_TAG] } });

    await mutateWhisparrMovie((movie) => ({ ...movie, tags: [tagId] }));
    await expectNoSearch(api, instanceId, SETTLE_MS);
  });

  test('skips an unmonitored movie when monitoredOnly is on', async ({ api }) => {
    const instanceId = await arrangeInstance(api, WHISPARR, { instance: { monitoredOnly: true } });

    await mutateWhisparrMovie((movie) => ({ ...movie, monitored: false }));
    await expectNoSearch(api, instanceId, SETTLE_MS);
  });

  test('searches a movie released long ago at the longest grace period', async ({ api }) => {
    const instanceId = await arrangeInstance(api, WHISPARR, { config: { postReleaseGraceHours: 72 } });
    await triggerSeeker(api);

    await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(1);
  });
});

test.describe('Seeker candidate filters on Lidarr', () => {
  let library: Array<Record<string, unknown>> = [];
  let albums: Array<Record<string, unknown>> = [];

  async function mutateLidarrArtist(mutate: (artist: Record<string, unknown>) => Record<string, unknown>): Promise<void> {
    const artist = await LIDARR.arr.get<Record<string, unknown>>(`/api/v1/artist/${LIDARR.itemId}`);
    await LIDARR.arr.put(`/api/v1/artist/${LIDARR.itemId}`, mutate({ ...artist }));
  }

  test.beforeEach(async () => {
    await resetLiveArrState();
    library = await snapshotLibrary(LIDARR);
    albums = await LIDARR.arr.get<Array<Record<string, unknown>>>(`/api/v1/album?artistId=${LIDARR.itemId}`);
    await indexerMock.stubMany(grabbableRelease(LIDARR.searchMode, LIDARR.release, LIDARR.category).mappings);
  });

  test.afterEach(async ({ api }) => {
    await teardownInstances(api);
    await restoreLibrary(LIDARR, library);
    for (const album of albums) {
      await LIDARR.arr.put(`/api/v1/album/${album.id}`, album);
    }
    await resetLiveArrState();
  });

  test('searches the seeded album when nothing filters it out', async ({ api }) => {
    const instanceId = await arrangeInstance(api, LIDARR);
    await triggerSeeker(api);

    await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(1);

    const event = await firstSearchEvent(api, instanceId);
    expect(event?.itemTitle).toContain(TEST_CONFIG.liveArr.seededAlbumTitle);
  });

  test('skips an artist carrying a skipped tag', async ({ api }) => {
    const tagId = await LIDARR.arr.ensureTag(SKIP_TAG);
    const instanceId = await arrangeInstance(api, LIDARR, { instance: { skipTags: [SKIP_TAG] } });

    await mutateLidarrArtist((artist) => ({ ...artist, tags: [tagId] }));
    await expectNoSearch(api, instanceId, SETTLE_MS);
  });

  test('skips an unmonitored album when monitoredOnly is on', async ({ api }) => {
    const instanceId = await arrangeInstance(api, LIDARR, { instance: { monitoredOnly: true } });

    for (const album of albums) {
      await LIDARR.arr.put(`/api/v1/album/${album.id}`, { ...album, monitored: false });
    }
    await expectNoSearch(api, instanceId, SETTLE_MS);
  });

  test('searches an album released long ago at the longest grace period', async ({ api }) => {
    const instanceId = await arrangeInstance(api, LIDARR, { config: { postReleaseGraceHours: 72 } });
    await triggerSeeker(api);

    await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(1);
  });
});
