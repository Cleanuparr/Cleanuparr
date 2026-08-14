import { test, expect } from '../fixtures/base';
import { indexerMock } from '../helpers/live-arr';
import {
  RADARR,
  TRIGGERED_SEARCH_TIMEOUT,
  arrangeInstance,
  listSearchEvents,
  resetLiveArrState,
  teardownInstances,
  triggerSeeker,
  waitForArrQueue,
} from '../helpers/seeker-live';
import { grabbableRelease } from '../helpers/mocks/torznab-stubs';

/**
 * The reactive half of the Seeker: a search queued by a removal.
 *
 * Nothing here is simulated. A real grab lands in a real queue, the queue
 * cleaner strikes it until it removes it, and that removal is what asks the
 * Seeker for a replacement.
 */

const SETTLE_MS = 30_000;

/** The rule's floor is above one, so the cleaner needs this many runs to remove. */
const MAX_STRIKES = 3;

test.describe('Seeker replacement searches', () => {
  const createdRules: string[] = [];

  test.beforeEach(async () => {
    await resetLiveArrState();
  });

  test.afterEach(async ({ api }) => {
    for (const id of createdRules.splice(0)) {
      await api.queueCleaner.deleteRule('stall', id);
    }

    await teardownInstances(api);
    await resetLiveArrState();
  });

  test('queues a replacement search when the queue cleaner removes the grab', async ({ api }) => {
    test.setTimeout(300_000);

    const release = grabbableRelease(RADARR.searchMode, RADARR.release, RADARR.category);
    await indexerMock.stubMany(release.mappings);

    const instanceId = await arrangeInstance(api, RADARR);
    await triggerSeeker(api);
    await waitForArrQueue(RADARR.arr, release.downloadId, TRIGGERED_SEARCH_TIMEOUT);

    const rule = await (
      await api.queueCleaner.createRule('stall', {
        name: 'e2e-live-stall',
        enabled: true,
        maxStrikes: MAX_STRIKES,
        privacyType: 'Both',
        minCompletionPercentage: 0,
        maxCompletionPercentage: 100,
        deletePrivateTorrentsFromClient: true,
        changeCategory: false,
        resetStrikesOnProgress: false,
        minimumProgress: null,
      })
    ).json();

    expect(rule.id, 'the stall rule should have been created').toBeTruthy();
    createdRules.push(rule.id);

    // One strike per run, and the removal happens on the run that reaches the limit.
    for (let strike = 0; strike <= MAX_STRIKES; strike++) {
      const triggered = await api.jobs.trigger('QueueCleaner');
      expect(triggered.status).toBeLessThan(300);
      await new Promise((resolve) => setTimeout(resolve, 3_000));
    }

    await expect
      .poll(
        async () => {
          await RADARR.arr.refreshMonitoredDownloads();
          return (await RADARR.arr.queue()).some((record) => record.downloadId === release.downloadId);
        },
        { timeout: SETTLE_MS },
      )
      .toBe(false);

    await triggerSeeker(api);

    await expect
      .poll(
        async () => (await listSearchEvents(api, instanceId)).map((event) => event.searchReason),
        { timeout: SETTLE_MS },
      )
      .toContain('Replacement');
  });
});
