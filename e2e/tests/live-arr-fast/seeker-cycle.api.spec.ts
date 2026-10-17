import { test, expect } from '../fixtures/base';
import type { CleanuparrApi } from '../helpers/api';
import { indexerMock } from '../helpers/live-arr';
import {
  RADARR,
  SONARR,
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

interface InstanceStat {
  instanceId: string;
  currentCycleId: string | null;
  cycleItemsSearched: number;
  cycleItemsTotal: number;
  cycleItemsRemaining: number;
  cycleStartedAt: string | null;
  cycleCompletedAt: string | null;
  lastCycleStartedAt: string | null;
  lastCycleCompletedAt: string | null;
}

async function instanceStat(api: CleanuparrApi, instanceId: string): Promise<InstanceStat | undefined> {
  const res = await api.seeker.getSearchStatsSummary();
  const body = (await res.json()) as { perInstanceStats: InstanceStat[] };
  return body.perInstanceStats.find((s) => s.instanceId === instanceId);
}

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

  test('reports a completed cycle while it waits out the minimum cycle time', async ({ api }) => {
    test.setTimeout(180_000);

    const instanceId = await arrangeInstance(api, RADARR, { instance: { minCycleTimeDays: 7 } });

    await triggerSeeker(api);
    await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(1);

    const searching = await instanceStat(api, instanceId);
    expect(searching?.cycleItemsSearched).toBe(searching?.cycleItemsTotal);
    expect(searching?.cycleItemsRemaining).toBe(0);
    expect(searching?.cycleStartedAt).not.toBeNull();
    expect(searching?.cycleCompletedAt).toBeNull();

    await triggerSeeker(api);
    await expect.poll(async () => (await instanceStat(api, instanceId))?.cycleCompletedAt ?? null, { timeout: SETTLE_MS }).not.toBeNull();

    const waiting = await instanceStat(api, instanceId);
    expect(waiting?.currentCycleId).toBe(searching?.currentCycleId);
    expect(await listSearchEvents(api, instanceId)).toHaveLength(1);
  });

  test('keeps the finished cycle as the last cycle once the next one starts', async ({ api }) => {
    test.setTimeout(180_000);

    const instanceId = await arrangeInstance(api, RADARR, { instance: { minCycleTimeDays: 0 } });

    await triggerSeeker(api);
    await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(1);
    const first = await instanceStat(api, instanceId);

    await triggerSeeker(api);
    await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(2);

    const second = await instanceStat(api, instanceId);
    expect(second?.currentCycleId).not.toBe(first?.currentCycleId);
    expect(second?.lastCycleStartedAt).toBe(first?.cycleStartedAt);
    expect(second?.lastCycleCompletedAt).not.toBeNull();
    expect(second?.cycleItemsSearched).toBe(1);
  });

  test('counts Sonarr cycle progress in seasons', async ({ api }) => {
    test.setTimeout(180_000);
    await indexerMock.stub(torznabSearchStub(SONARR.searchMode, []));

    const missing = await SONARR.arr.get<{ records: Array<{ seasonNumber: number; airDateUtc?: string }> }>(
      '/api/v3/wanted/missing?pageSize=1000&monitored=true',
    );
    const airedSeasons = new Set(
      missing.records
        .filter((e) => e.airDateUtc && Date.parse(e.airDateUtc) < Date.now() - 3 * 24 * 3600 * 1000)
        .map((e) => e.seasonNumber),
    );

    const instanceId = await arrangeInstance(api, SONARR, { instance: { minCycleTimeDays: 7 } });

    await triggerSeeker(api);
    await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(1);

    const stat = await instanceStat(api, instanceId);
    expect(stat?.cycleItemsTotal).toBe(airedSeasons.size);
    expect(stat?.cycleItemsRemaining).toBe(airedSeasons.size - 1);
  });
});
