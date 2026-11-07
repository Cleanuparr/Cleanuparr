import { test, expect } from '../fixtures/base';
import { indexerMock } from '../helpers/live-arr';
import {
  RADARR,
  TRIGGERED_SEARCH_TIMEOUT,
  arrangeInstance,
  arrangeQueueCleaner,
  createStallRule,
  expectNoSearch,
  listSearchEvents,
  resetLiveArrState,
  teardownInstances,
  teardownQueueCleaner,
  triggerSeeker,
  waitForArrQueue,
} from '../helpers/seeker-live';
import { grabbableRelease } from '../helpers/mocks/torznab-stubs';

/** The app container runs on UTC, so the windows use the UTC clock. */

const MINUTES_PER_DAY = 1440;
const SETTLE_MS = 30_000;
const MAX_STRIKES = 3;

/** HH:mm:00 of the UTC clock shifted by the given minutes, wrapping past midnight. */
function utcTime(offsetMinutes: number): string {
  const now = new Date();
  const total = now.getUTCHours() * 60 + now.getUTCMinutes() + offsetMinutes;
  const wrapped = ((total % MINUTES_PER_DAY) + MINUTES_PER_DAY) % MINUTES_PER_DAY;
  const hours = String(Math.floor(wrapped / 60)).padStart(2, '0');
  const minutes = String(wrapped % 60).padStart(2, '0');

  return `${hours}:${minutes}:00`;
}

function quietWindow(startOffset: number, endOffset: number): Record<string, unknown> {
  return {
    quietHoursEnabled: true,
    quietHoursStart: utcTime(startOffset),
    quietHoursEnd: utcTime(endOffset),
  };
}

test.describe('Seeker quiet hours', () => {
  test.beforeEach(async () => {
    await resetLiveArrState();
    await indexerMock.stubMany(grabbableRelease(RADARR.searchMode, RADARR.release, RADARR.category).mappings);
  });

  test.afterEach(async ({ api }) => {
    await teardownQueueCleaner(api);
    await teardownInstances(api);
    await resetLiveArrState();
  });

  test('skips proactive search inside the window', async ({ api }) => {
    const instanceId = await arrangeInstance(api, RADARR, { config: quietWindow(-60, 60) });

    await expectNoSearch(api, instanceId);
  });

  test('searches outside the window', async ({ api }) => {
    const instanceId = await arrangeInstance(api, RADARR, { config: quietWindow(60, 120) });

    await triggerSeeker(api);

    await expect
      .poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: TRIGGERED_SEARCH_TIMEOUT })
      .toBe(1);
  });

  test('still runs replacement search inside the window', async ({ api }) => {
    test.setTimeout(300_000);

    const release = grabbableRelease(RADARR.searchMode, RADARR.release, RADARR.category);
    await arrangeQueueCleaner(api);

    const instanceId = await arrangeInstance(api, RADARR);
    await triggerSeeker(api);
    await waitForArrQueue(RADARR.arr, release.downloadId, TRIGGERED_SEARCH_TIMEOUT);

    await createStallRule(api, MAX_STRIKES);

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

    const config = await (await api.seeker.getConfig()).json();
    const updated = await api.seeker.updateConfig({ ...config, ...quietWindow(-60, 60) });
    expect(updated.ok).toBe(true);

    await triggerSeeker(api);

    await expect
      .poll(
        async () => (await listSearchEvents(api, instanceId)).map((event) => event.searchReason),
        { timeout: SETTLE_MS },
      )
      .toContain('Replacement');
  });
});
