import { test, expect } from '../fixtures/base';
import { indexerMock } from '../helpers/live-arr';
import {
  RUN_TAG,
  SPORTARR,
  SPORTARR_EVENTS,
  TRIGGERED_SEARCH_TIMEOUT,
  arrangeInstance,
  arrangeQueueCleaner,
  createStallRule,
  isolateSportarrGrabbableEvent,
  listSearchEvents,
  resetLiveArrState,
  restoreSportarrEvents,
  teardownInstances,
  teardownQueueCleaner,
  triggerSeeker,
  waitForArrQueue,
} from '../helpers/seeker-live';
import { grabbableRelease } from '../helpers/mocks/torznab-stubs';

/**
 * Sportarr ignores every v3 command except ManualImport,
 * so a replacement search only works through its native search queue.
 * Each search leaves a task behind, which is how this spec tells one fired.
 */

const SETTLE_MS = 30_000;

/** The rule's floor is above one, so the cleaner needs this many runs to remove. */
const MAX_STRIKES = 3;

// Cleanuparr deletes with blocklist=true, so a later spec grabbing SPORTARR.release would be rejected.
const RELEASE = `UFC Fight Night 232 Allen vs Craig 1080p WEB h264-E2ER${RUN_TAG}`;

async function eventSearchTasks(): Promise<number> {
  const tasks = await SPORTARR.arr.get<Array<{ commandName: string; body: string }>>('/api/task');
  return tasks.filter((task) => task.commandName === 'EventSearch' && task.body === String(SPORTARR_EVENTS.grabbable))
    .length;
}

test.describe('Sportarr replacement search', () => {
  test.beforeEach(async () => {
    await resetLiveArrState();
  });

  test.afterEach(async ({ api }) => {
    await teardownQueueCleaner(api);
    await teardownInstances(api);
    await restoreSportarrEvents();
    await resetLiveArrState();
  });

  test('fires a native Sportarr search when the queue cleaner removes the grab', async ({ api }) => {
    test.setTimeout(300_000);

    const release = grabbableRelease(SPORTARR.searchMode, RELEASE, SPORTARR.category);
    await indexerMock.stubMany(release.mappings);

    await arrangeQueueCleaner(api);
    await isolateSportarrGrabbableEvent();

    const instanceId = await arrangeInstance(api, SPORTARR);
    await triggerSeeker(api);
    await waitForArrQueue(SPORTARR.arr, release.downloadId, TRIGGERED_SEARCH_TIMEOUT);

    const tasksBeforeRemoval = await eventSearchTasks();
    expect(tasksBeforeRemoval).toBeGreaterThan(0);

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
          await SPORTARR.arr.refreshMonitoredDownloads();
          return (await SPORTARR.arr.queue()).some((record) => record.downloadId.toUpperCase() === release.downloadId);
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

    await expect.poll(eventSearchTasks, { timeout: SETTLE_MS }).toBeGreaterThan(tasksBeforeRemoval);
  });
});
