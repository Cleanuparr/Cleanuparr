import { test, expect } from '../fixtures/base';
import type { CleanuparrApi } from '../helpers/api';
import { indexerMock } from '../helpers/live-arr';
import {
  RADARR,
  SONARR,
  addDecoyDownloads,
  arrangeInstance,
  arrangeQueueCleaner,
  createStallRule,
  expectNoSearch,
  firstSearchEvent,
  listSearchEvents,
  resetLiveArrState,
  restoreLibrary,
  snapshotLibrary,
  teardownInstances,
  teardownQueueCleaner,
  triggerSeeker,
  RUN_TAG,
  TRIGGERED_SEARCH_TIMEOUT,
} from '../helpers/seeker-live';
import { grabbableRelease, grabbableReleases } from '../helpers/mocks/torznab-stubs';

/**
 * The per-instance filters that decide whether the Seeker searches at all.
 *
 * Each test drives a real arr, so the library state comes from real API calls.
 * The teardown puts the library back, whatever a test did to it.
 */

const SKIP_TAG = 'e2e-seeker-skip';

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

  test('skips a movie carrying a skipped tag', async ({ api }) => {
    const tagId = await RADARR.arr.ensureTag(SKIP_TAG);
    const instanceId = await arrangeInstance(api, RADARR, { instance: { skipTags: [SKIP_TAG] } });

    await mutateMovie((movie) => ({ ...movie, tags: [tagId] }));
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
});
