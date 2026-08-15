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
import { appLogsSince } from '../helpers/test-lifecycle';

/**
 * The same grab, driven by an on-demand trigger instead of the cron schedule.
 *
 * This is the canary for the patched image.
 * If the trigger is rejected the whole folder fails immediately, not by timeout.
 */

/**
 * What the monitor logs when it cannot read the arr's command list.
 *
 * It then polls each command on its own and reaches the same event status,
 * so the log is the only place a broken list request shows up.
 */
const LIST_FALLBACK = 'Failed to list commands on';

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

      const startedAt = new Date().toISOString();
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

      const logs = appLogsSince(startedAt);
      expect(logs, `the monitor could not read the ${target.type} command list`).not.toContain(LIST_FALLBACK);
    });
  }
});
