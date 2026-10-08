import { test, expect } from '../fixtures/base';
import type { CleanuparrApi } from '../helpers/api';
import { indexerMock } from '../helpers/live-arr';
import {
  RADARR,
  arrangeInstance,
  arrangeQueueCleaner,
  createStallRule,
  resetLiveArrState,
  restoreLibrary,
  snapshotLibrary,
  teardownInstances,
  teardownQueueCleaner,
  triggerSeeker,
  TRIGGERED_SEARCH_TIMEOUT,
} from '../helpers/seeker-live';
import { grabbableRelease } from '../helpers/mocks/torznab-stubs';

/**
 * Disabling dry run queues a purge that deletes every dry-run strike and event soon after.
 *
 * A real stall strike needs a real grabbed download, so these specs drive the live arr
 * the same way `seeker-filters.api.spec.ts` does: search, grab, then strike the stall.
 */

/** A strike shows up within a couple of cycles, so this is generous. */
const STRIKE_TIMEOUT = 45_000;

/** The purge runs within milliseconds to a few seconds of the config save. */
const PURGE_TIMEOUT = 20_000;

/** The stall rule floor is above two, so two strikes never remove the download. */
const MAX_STRIKES = 3;

interface StrikeGroup {
  downloadId: string;
  totalStrikes: number;
  hasDryRunStrikes: boolean;
}

/** The strikes API lowercases downloadId, but the arr queue reports it uppercase. */
async function strikeGroup(api: CleanuparrApi, downloadId: string): Promise<StrikeGroup | undefined> {
  const body = await (await api.strikes.list({ pageSize: 200 })).json();
  return (body.items ?? []).find(
    (item: StrikeGroup) => item.downloadId.toLowerCase() === downloadId.toLowerCase(),
  );
}

/** Searches the seeded movie and waits for the grab to reach the arr's queue. */
async function grabRelease(api: CleanuparrApi): Promise<void> {
  await triggerSeeker(api);
  await expect
    .poll(
      async () => {
        await RADARR.arr.refreshMonitoredDownloads();
        return (await RADARR.arr.queue()).filter((record) => record.sizeleft > 0).length;
      },
      { timeout: TRIGGERED_SEARCH_TIMEOUT },
    )
    .toBeGreaterThan(0);
}

/** The downloadId of the single stalled item the grab left in the queue. */
async function stalledDownloadId(): Promise<string> {
  const stalled = (await RADARR.arr.queue()).find((record) => record.sizeleft > 0);
  expect(stalled, 'expected a stalled download in the queue').toBeTruthy();
  return stalled!.downloadId;
}

/** Triggers the queue cleaner until the download's strike count reaches the target. */
async function strikeUntil(api: CleanuparrApi, downloadId: string, total: number): Promise<void> {
  for (let attempt = 0; attempt < 3; attempt++) {
    await api.jobs.trigger('QueueCleaner');

    const reached = await expect
      .poll(async () => (await strikeGroup(api, downloadId))?.totalStrikes ?? 0, {
        timeout: STRIKE_TIMEOUT,
        intervals: [2_000],
      })
      .toBeGreaterThanOrEqual(total)
      .then(
        () => true,
        () => false,
      );

    if (reached) {
      return;
    }
  }

  throw new Error(`the queue cleaner never struck download ${downloadId} up to ${total}`);
}

test.describe('Dry run purge', () => {
  let library: Array<Record<string, unknown>> = [];

  test.beforeEach(async () => {
    await resetLiveArrState();
    library = await snapshotLibrary(RADARR);
    await indexerMock.stubMany(grabbableRelease(RADARR.searchMode, RADARR.release, RADARR.category).mappings);
  });

  test.afterEach(async ({ api }) => {
    await api.general.patch({ dryRun: false });
    await teardownQueueCleaner(api);
    await teardownInstances(api);
    await restoreLibrary(RADARR, library);
    await resetLiveArrState();
  });

  test('disabling dry run purges dry strikes and events', async ({ api }) => {
    test.setTimeout(240_000);

    await arrangeInstance(api, RADARR);
    await arrangeQueueCleaner(api);
    await createStallRule(api, MAX_STRIKES);

    await grabRelease(api);
    const downloadId = await stalledDownloadId();

    await api.general.patch({ dryRun: true });
    await strikeUntil(api, downloadId, 1);

    await api.general.patch({ dryRun: false });

    await expect
      .poll(async () => (await strikeGroup(api, downloadId)) === undefined, { timeout: PURGE_TIMEOUT })
      .toBe(true);
    await expect
      .poll(
        async () => {
          const events = await (await api.events.list({ pageSize: 200 })).json();
          return (events.items ?? []).some((event: { isDryRun: boolean }) => event.isDryRun);
        },
        { timeout: PURGE_TIMEOUT },
      )
      .toBe(false);
  });

  test('live strikes survive the purge', async ({ api }) => {
    test.setTimeout(240_000);

    await arrangeInstance(api, RADARR);
    await arrangeQueueCleaner(api);
    await createStallRule(api, MAX_STRIKES);

    await grabRelease(api);
    const downloadId = await stalledDownloadId();

    // Live strike first, so the purge has one to keep.
    await strikeUntil(api, downloadId, 1);

    await api.general.patch({ dryRun: true });
    await strikeUntil(api, downloadId, 2);

    await api.general.patch({ dryRun: false });

    await expect
      .poll(
        async () => {
          const group = await strikeGroup(api, downloadId);
          return group ? { total: group.totalStrikes, dry: group.hasDryRunStrikes } : undefined;
        },
        { timeout: PURGE_TIMEOUT },
      )
      .toEqual({ total: 1, dry: false });
  });
});
