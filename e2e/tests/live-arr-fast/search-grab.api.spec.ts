import { test, expect } from '../fixtures/base';
import { indexerMock } from '../helpers/live-arr';
import {
  RADARR,
  SONARR,
  TRANSITION_TIMEOUT,
  TRIGGERED_SEARCH_TIMEOUT,
  arrangeInstance,
  firstSearchEvent,
  resetLiveArrState,
  teardownInstances,
  triggerSeeker,
  waitForArrQueue,
} from '../helpers/seeker-live';
import { grabbableRelease } from '../helpers/mocks/torznab-stubs';

/**
 * The same grab, driven by an on-demand trigger instead of the cron schedule.
 *
 * This is the canary for the patched image.
 * If the trigger is rejected the whole folder fails immediately, not by timeout.
 */

const TEST_TIMEOUT = TRIGGERED_SEARCH_TIMEOUT + TRANSITION_TIMEOUT + 120_000;

test.describe('Seeker grab on demand', () => {
  test.beforeEach(async () => {
    await resetLiveArrState();
  });

  test.afterEach(async ({ api }) => {
    await teardownInstances(api);
    await resetLiveArrState();
  });

  for (const target of [SONARR, RADARR]) {
    test(`records the real ${target.type} grab on the search event`, async ({ api }) => {
      test.setTimeout(TEST_TIMEOUT);

      const release = grabbableRelease(target.searchMode, target.release, target.category);
      await indexerMock.stubMany(release.mappings);

      const instanceId = await arrangeInstance(api, target);
      await triggerSeeker(api);

      await waitForArrQueue(target.arr, release.downloadId, TRIGGERED_SEARCH_TIMEOUT);

      await expect
        .poll(async () => (await firstSearchEvent(api, instanceId))?.searchStatus, { timeout: TRANSITION_TIMEOUT })
        .toBe('Completed');

      const event = await firstSearchEvent(api, instanceId);
      expect(event?.grabbedItems).toContain(release.title);
    });
  }
});
