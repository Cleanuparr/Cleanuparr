import { test, expect } from '../fixtures/base';
import type { CleanuparrApi } from '../helpers/api';
import { indexerMock } from '../helpers/live-arr';
import {
  RADARR,
  arrangeInstance,
  expectNoSearch,
  listSearchEvents,
  resetLiveArrState,
  restoreLibrary,
  snapshotLibrary,
  teardownInstances,
  triggerSeeker,
  updateInstance,
} from '../helpers/seeker-live';
import { torznabSearchStub } from '../helpers/mocks/torznab-stubs';

/**
 * A unit rests after three searches in a row grab nothing.
 *
 * The indexer returns nothing, so every search misses.
 * The command monitor records each outcome after the search command completes.
 */

const SETTLE_MS = 20_000;

const MISSES_BEFORE_REST = 3;

interface Preview {
  funnel: { restingUnits: number };
  nextUp: Array<{ name: string }>;
  excluded: Array<{
    name: string;
    reason: string;
    consecutiveMisses: number | null;
    restCyclesLeft: number | null;
    restUntil: string | null;
  }>;
}

async function getPreview(api: CleanuparrApi, instanceId: string): Promise<Preview> {
  const res = await api.seeker.getPreview(instanceId);
  expect(res.status, await res.clone().text()).toBe(200);
  return (await res.json()) as Preview;
}

async function seededExclusion(api: CleanuparrApi, instanceId: string): Promise<Preview['excluded'][number] | undefined> {
  return (await getPreview(api, instanceId)).excluded.find((e) => e.name.includes(RADARR.seededTitle));
}

/** Searches the movie until it rests, keeping the cycle open between searches with a reset. */
async function searchUntilResting(api: CleanuparrApi, instanceId: string): Promise<void> {
  for (let miss = 1; miss <= MISSES_BEFORE_REST; miss++) {
    await triggerSeeker(api);
    await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(miss);

    await expect
      .poll(async () => (await listSearchEvents(api, instanceId))[0].searchStatus, { timeout: SETTLE_MS })
      .toBe('Completed');
    expect((await listSearchEvents(api, instanceId))[0].grabbedItems ?? []).toHaveLength(0);

    // The search event is published while the run still holds the instance, so the reset can be refused as busy.
    await expect.poll(async () => (await api.seeker.resetCycle(instanceId)).status, { timeout: SETTLE_MS }).toBe(204);
  }

  await expect.poll(async () => (await seededExclusion(api, instanceId))?.reason, { timeout: SETTLE_MS }).toBe('Resting');
}

test.describe('Seeker unit rest', () => {
  let library: Array<Record<string, unknown>> = [];

  test.beforeEach(async () => {
    await resetLiveArrState();
    library = await snapshotLibrary(RADARR);
    await indexerMock.stub(torznabSearchStub(RADARR.searchMode, []));
  });

  test.afterEach(async ({ api }) => {
    await teardownInstances(api);
    await restoreLibrary(RADARR, library);
    await resetLiveArrState();
  });

  test('rests a movie after three searches find nothing', async ({ api }) => {
    test.setTimeout(240_000);

    const instanceId = await arrangeInstance(api, RADARR, {
      instance: { restUnitsEnabled: true, restMaxDays: 30, minCycleTimeDays: 7 },
    });

    await searchUntilResting(api, instanceId);

    const preview = await getPreview(api, instanceId);
    const rested = preview.excluded.find((e) => e.name.includes(RADARR.seededTitle));
    expect(rested?.reason).toBe('Resting');
    expect(rested?.consecutiveMisses).toBe(MISSES_BEFORE_REST);
    expect(rested?.restCyclesLeft).toBeGreaterThanOrEqual(1);
    expect(rested?.restUntil).not.toBeNull();
    expect(preview.funnel.restingUnits).toBe(1);

    await expectNoSearch(api, instanceId, SETTLE_MS, MISSES_BEFORE_REST);
  });

  test('searches a resting movie again once rest is turned off', async ({ api }) => {
    test.setTimeout(240_000);

    const instanceId = await arrangeInstance(api, RADARR, {
      instance: { restUnitsEnabled: true, restMaxDays: 30, minCycleTimeDays: 7 },
    });

    await searchUntilResting(api, instanceId);
    const before = (await listSearchEvents(api, instanceId)).length;

    await updateInstance(api, instanceId, { restUnitsEnabled: false });

    await triggerSeeker(api);
    await expect
      .poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS })
      .toBe(before + 1);
  });
});
