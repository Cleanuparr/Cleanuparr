import { test, expect } from '../fixtures/base';
import type { CleanuparrApi } from '../helpers/api';
import { indexerMock } from '../helpers/live-arr';
import {
  RADARR,
  RUN_TAG,
  SONARR,
  TRIGGERED_SEARCH_TIMEOUT,
  arrangeInstance,
  listSearchEvents,
  resetLiveArrState,
  teardownInstances,
  triggerSeeker,
  waitForArrQueue,
} from '../helpers/seeker-live';
import { grabbableRelease, torznabSearchStub } from '../helpers/mocks/torznab-stubs';

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

  test('searches the same item again after a reset despite the minimum cycle time', async ({ api }) => {
    test.setTimeout(180_000);

    const instanceId = await arrangeInstance(api, RADARR, { instance: { minCycleTimeDays: 7 } });

    await triggerSeeker(api);
    await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(1);
    const first = await instanceStat(api, instanceId);

    // The search event is published while the run still holds the instance, so the reset can be refused as busy.
    await expect.poll(async () => (await api.seeker.resetCycle(instanceId)).status, { timeout: SETTLE_MS }).toBe(204);

    await triggerSeeker(api);
    await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(2);

    const afterReset = await instanceStat(api, instanceId);
    expect(afterReset?.currentCycleId).not.toBe(first?.currentCycleId);
    expect(afterReset?.lastCycleStartedAt).toBe(first?.cycleStartedAt);
    expect(afterReset?.lastCycleCompletedAt).not.toBeNull();
    expect(afterReset?.cycleItemsSearched).toBe(1);
  });

  test('refuses to reset a cycle that has searched nothing', async ({ api }) => {
    const instanceId = await arrangeInstance(api, RADARR, { instance: { minCycleTimeDays: 7 } });

    const res = await api.seeker.resetCycle(instanceId);
    expect(res.status).toBe(409);
    expect((await res.json()).detail).toContain('has not searched anything yet');
  });

  test('completes the cycle once the only remaining movie is queued', async ({ api }) => {
    test.setTimeout(180_000);

    const release = grabbableRelease(RADARR.searchMode, `F1.2025.1080p.WEB-DL.DDP5.1.H.264-E2E${RUN_TAG}`, RADARR.category);
    await indexerMock.resetAll();
    await indexerMock.stubMany(release.mappings);

    // minCycleTimeDays keeps the same cycle alive across both triggers below.
    // At 0 a new cycle would start on the second trigger instead of exhausting this one.
    const instanceId = await arrangeInstance(api, RADARR, { instance: { minCycleTimeDays: 7 } });

    await triggerSeeker(api);
    await waitForArrQueue(RADARR.arr, release.downloadId, TRIGGERED_SEARCH_TIMEOUT);

    const searching = await instanceStat(api, instanceId);
    expect(searching?.cycleItemsSearched).toBe(1);
    expect(searching?.cycleCompletedAt).toBeNull();

    // The movie is now queued, so the next run has nothing left to search.
    await triggerSeeker(api);
    await expect
      .poll(async () => (await instanceStat(api, instanceId))?.cycleCompletedAt ?? null, { timeout: SETTLE_MS })
      .not.toBeNull();

    const completed = await instanceStat(api, instanceId);
    expect(completed?.currentCycleId).toBe(searching?.currentCycleId);
    expect(completed?.cycleItemsSearched).toBe(1);
  });

  test('counts only the movie the Seeker itself searched', async ({ api }) => {
    test.setTimeout(180_000);

    // The seed's second movie, grabbed directly against the arr so it sits queued
    // without the Seeker ever searching it this cycle.
    const others = (await RADARR.arr.get<Array<{ id: number }>>('/api/v3/movie')).filter(
      (movie) => movie.id !== RADARR.itemId,
    );
    const secondMovieId = others[0].id;

    const secondRelease = grabbableRelease(
      RADARR.searchMode,
      `The.Wild.Robot.2024.1080p.WEB-DL.DDP5.1.H.264-E2E${RUN_TAG}`,
      RADARR.category,
    );
    await indexerMock.resetAll();
    await indexerMock.stubMany(secondRelease.mappings);
    await RADARR.arr.post('/api/v3/command', { name: 'MoviesSearch', movieIds: [secondMovieId] });
    await waitForArrQueue(RADARR.arr, secondRelease.downloadId, TRIGGERED_SEARCH_TIMEOUT);

    // The Seeker's own search returns nothing, so the queue above is the only exclusion.
    await indexerMock.resetAll();
    await indexerMock.stub(torznabSearchStub(RADARR.searchMode, []));

    const instanceId = await arrangeInstance(api, RADARR, {
      instance: { minCycleTimeDays: 0, monitoredOnly: false },
    });

    await triggerSeeker(api);
    await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(1);

    const stat = await instanceStat(api, instanceId);
    expect(stat?.cycleItemsTotal).toBe(2);
    expect(stat?.cycleItemsSearched).toBe(1);
    expect(stat?.cycleItemsRemaining).toBe(0);
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
