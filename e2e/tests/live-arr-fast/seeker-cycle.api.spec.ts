import { test, expect } from '../fixtures/base';
import { indexerMock } from '../helpers/live-arr';
import {
  RADARR,
  arrangeInstance,
  listSearchEvents,
  resetLiveArrState,
  teardownInstances,
  triggerSeeker,
} from '../helpers/seeker-live';
import { torznabSearchStub } from '../helpers/mocks/torznab-stubs';

/**
 * What happens once every eligible item has been searched.
 *
 * The indexer returns nothing on purpose.
 * A grab would put the item in the queue and exclude it from the next run,
 * which would hide whether the cycle logic did the excluding.
 */

const SETTLE_MS = 20_000;

test.describe('Seeker search cycles', () => {
  test.beforeEach(async () => {
    await resetLiveArrState();
    await indexerMock.stub(torznabSearchStub(RADARR.searchMode, []));
  });

  test.afterEach(async ({ api }) => {
    await teardownInstances(api);
    await resetLiveArrState();
  });

  test('starts a new cycle when no minimum cycle time is set', async ({ api }) => {
    test.setTimeout(180_000);

    const instanceId = await arrangeInstance(api, RADARR, { instance: { minCycleTimeDays: 0 } });

    await triggerSeeker(api);
    await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(1);

    await triggerSeeker(api);
    await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(2);
  });

  test('waits out the minimum cycle time before searching the same item again', async ({ api }) => {
    test.setTimeout(180_000);

    const instanceId = await arrangeInstance(api, RADARR, { instance: { minCycleTimeDays: 7 } });

    await triggerSeeker(api);
    await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(1);

    await triggerSeeker(api);
    await new Promise((resolve) => setTimeout(resolve, SETTLE_MS));

    expect(await listSearchEvents(api, instanceId)).toHaveLength(1);
  });
});
