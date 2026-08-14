import { test, expect } from '../fixtures/base';
import { indexerMock } from '../helpers/live-arr';
import {
  RADARR,
  SCHEDULED_SEARCH_TIMEOUT,
  SONARR,
  TRANSITION_TIMEOUT,
  arrangeInstance,
  firstSearchEvent,
  resetLiveArrState,
  teardownInstances,
  waitForArrQueue,
} from '../helpers/seeker-live';
import { grabbableRelease, torznabSearchStub } from '../helpers/mocks/torznab-stubs';

/**
 * The Seeker against real Sonarr and Radarr, on the shipped image.
 *
 * Only the indexer is faked.
 * Every run here waits for the real cron schedule, which is the point of this folder.
 * The behaviour matrix lives in tests/live-arr-fast, which triggers runs on demand.
 */

const TEST_TIMEOUT = SCHEDULED_SEARCH_TIMEOUT + TRANSITION_TIMEOUT + 120_000;

/** Every arr command state Cleanuparr knows how to map. */
const KNOWN_COMMAND_STATES = ['queued', 'started', 'completed', 'failed', 'aborted', 'cancelled', 'orphaned'];

test.describe('Seeker against a live arr', () => {
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

      await waitForArrQueue(target.arr, release.downloadId, SCHEDULED_SEARCH_TIMEOUT);

      await expect
        .poll(async () => (await firstSearchEvent(api, instanceId))?.searchStatus, { timeout: TRANSITION_TIMEOUT })
        .toBe('Completed');

      const event = await firstSearchEvent(api, instanceId);
      expect(event?.grabbedItems).toContain(release.title);
    });
  }

  test('completes the search event when the indexer returns nothing', async ({ api }) => {
    test.setTimeout(TEST_TIMEOUT);

    await indexerMock.stub(torznabSearchStub(SONARR.searchMode, []));

    const instanceId = await arrangeInstance(api, SONARR);

    await expect
      .poll(async () => (await firstSearchEvent(api, instanceId))?.searchStatus, {
        timeout: SCHEDULED_SEARCH_TIMEOUT + TRANSITION_TIMEOUT,
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
