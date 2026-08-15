import { test, expect } from '../fixtures/base';
import { indexerMock } from '../helpers/live-arr';
import {
  RADARR,
  addDecoyDownloads,
  arrangeInstance,
  expectNoSearch,
  listSearchEvents,
  resetLiveArrState,
  teardownInstances,
  triggerSeeker,
} from '../helpers/seeker-live';
import { grabbableRelease } from '../helpers/mocks/torznab-stubs';

/**
 * Reading an arr queue that does not fit on one page.
 *
 * Cleanuparr asks for 200 records at a time and walks the rest by page.
 * The active download limit is the assertion: it can only trip on a count that
 * includes the record beyond the first page.
 */

/** One more than the arr's page size, so exactly one record lands on page two. */
const OVER_ONE_PAGE = 201;

const TEST_TIMEOUT = 600_000;
const SETTLE_MS = 30_000;

test.describe('Seeker over a paged arr queue', () => {
  test.beforeEach(async () => {
    await resetLiveArrState();
    await indexerMock.stubMany(grabbableRelease(RADARR.searchMode, RADARR.release, RADARR.category).mappings);
  });

  test.afterEach(async ({ api }) => {
    await teardownInstances(api);
    await resetLiveArrState();
  });

  test('the arr pages its queue the way Cleanuparr expects', async ({ api }) => {
    test.setTimeout(TEST_TIMEOUT);

    await arrangeInstance(api, RADARR);
    await addDecoyDownloads(RADARR, OVER_ONE_PAGE);

    const first = await RADARR.arr.queuePage(1);
    expect(first.totalRecords).toBeGreaterThanOrEqual(OVER_ONE_PAGE);
    expect(first.records ?? []).toHaveLength(200);

    const second = await RADARR.arr.queuePage(2);
    expect((second.records ?? []).length).toBeGreaterThan(0);
  });

  test('counts active downloads from every page of the queue', async ({ api }) => {
    test.setTimeout(TEST_TIMEOUT);

    // Only a count that reaches page two hits this limit.
    const instanceId = await arrangeInstance(api, RADARR, {
      instance: { activeDownloadLimit: OVER_ONE_PAGE },
    });

    await addDecoyDownloads(RADARR, OVER_ONE_PAGE);
    await expectNoSearch(api, instanceId, SETTLE_MS);
  });

  test('searches when the paged queue stays under the limit', async ({ api }) => {
    test.setTimeout(TEST_TIMEOUT);

    // Far above the decoy count, because the test before this one pins the boundary.
    const instanceId = await arrangeInstance(api, RADARR, {
      instance: { activeDownloadLimit: OVER_ONE_PAGE * 2 },
    });

    await addDecoyDownloads(RADARR, OVER_ONE_PAGE);
    await triggerSeeker(api);

    await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(1);
  });
});
