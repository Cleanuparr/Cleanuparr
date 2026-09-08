import { test, expect } from '../fixtures/base';
import { indexerMock } from '../helpers/live-arr';
import {
  RUN_TAG,
  SONARR,
  TRANSITION_TIMEOUT,
  TRIGGERED_SEARCH_TIMEOUT,
  arrangeInstance,
  arrangeQueueCleaner,
  createStallRule,
  listSearchEvents,
  resetLiveArrState,
  teardownInstances,
  teardownQueueCleaner,
  triggerSeeker,
  waitForArrQueue,
} from '../helpers/seeker-live';
import { grabbableRelease } from '../helpers/mocks/torznab-stubs';

/**
 * What a replacement search records once the arr grabs for it.
 *
 * Cover for #774, so it deliberately avoids S01E01.
 * The seed gives that episode id 1, the same number as its series,
 * and a series id then reads the same as an episode id.
 * S01E02 has episode id 2, which tells the two apart.
 */

const SETTLE_MS = 30_000;

/** The rule's floor is above one, so the cleaner needs this many runs to remove. */
const MAX_STRIKES = 3;

const EPISODE = 'Agatha.All.Along.S01E02.1080p.WEB-DL.DDP5.1.H.264';

test.describe('Seeker replacement grabs', () => {
  test.beforeEach(async () => {
    await resetLiveArrState();
  });

  test.afterEach(async ({ api }) => {
    await teardownQueueCleaner(api);
    await teardownInstances(api);
    await resetLiveArrState();
  });

  test('records the Sonarr grab on the replacement search event', async ({ api }) => {
    test.setTimeout(300_000);

    const first = grabbableRelease(SONARR.searchMode, `${EPISODE}-E2EA${RUN_TAG}`, SONARR.category);
    await indexerMock.stubMany(first.mappings);

    await arrangeQueueCleaner(api);

    const instanceId = await arrangeInstance(api, SONARR);
    await triggerSeeker(api);
    await waitForArrQueue(SONARR.arr, first.downloadId, TRIGGERED_SEARCH_TIMEOUT);

    await createStallRule(api, MAX_STRIKES);

    // One strike per run, and the removal happens on the run that reaches the limit.
    for (let strike = 0; strike <= MAX_STRIKES; strike++) {
      const triggered = await api.jobs.trigger('QueueCleaner');
      expect(triggered.status).toBeLessThan(300);
      await new Promise((resolve) => setTimeout(resolve, 3_000));
    }

    await expect
      .poll(
        async () => {
          await SONARR.arr.refreshMonitoredDownloads();
          return (await SONARR.arr.queue()).some((record) => record.downloadId === first.downloadId);
        },
        { timeout: SETTLE_MS },
      )
      .toBe(false);

    // Cleanuparr deletes with blocklist=true, so the replacement needs a release of its own.
    const replacement = grabbableRelease(SONARR.searchMode, `${EPISODE}-E2EB${RUN_TAG}`, SONARR.category);
    await indexerMock.resetAll();
    await indexerMock.stubMany(replacement.mappings);

    await triggerSeeker(api);
    await waitForArrQueue(SONARR.arr, replacement.downloadId, TRIGGERED_SEARCH_TIMEOUT);

    await expect
      .poll(
        async () => {
          const events = await listSearchEvents(api, instanceId);
          return events.find((event) => event.searchReason === 'Replacement')?.searchStatus;
        },
        { timeout: TRANSITION_TIMEOUT },
      )
      .toBe('Completed');

    const events = await listSearchEvents(api, instanceId);
    const replacementEvent = events.find((event) => event.searchReason === 'Replacement');

    expect(replacementEvent?.grabbedItems).toContain(replacement.title);
  });
});
