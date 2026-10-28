import { test, expect } from '../fixtures/base';
import type { CleanuparrApi } from '../helpers/api';
import { arrMoviesStub } from '../helpers/mocks/arr-stubs';
import { arrangeSearchableInstance, teardownSearchableInstances } from '../helpers/seeker-fast';
import { listSearchEvents, triggerSeeker } from '../helpers/seeker-live';

const SETTLE_MS = 30_000;

/** Keeps the run inside the locked pass while the reset goes out, under the arr HttpClient timeout (100s by default). */
const LIBRARY_DELAY_MS = 8_000;

async function currentCycleId(api: CleanuparrApi, instanceId: string): Promise<string | null | undefined> {
  const body = (await (await api.seeker.getSearchStatsSummary()).json()) as {
    perInstanceStats: Array<{ instanceId: string; currentCycleId: string | null }>;
  };
  return body.perInstanceStats.find((stat) => stat.instanceId === instanceId)?.currentCycleId;
}

test.describe('Seeker: cycle reset during a run', () => {
  test.afterEach(async ({ api }) => {
    await teardownSearchableInstances(api);
  });

  test('refuses a reset while the Seeker is searching the instance, then accepts it', async ({ api, mocks }) => {
    test.setTimeout(120_000);

    const title = 'Reset During Run';
    const instanceId = await arrangeSearchableInstance(api, mocks, title, [], { minCycleTimeDays: 7 });

    await triggerSeeker(api);
    await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(1);

    const cycleBefore = await currentCycleId(api, instanceId);
    expect(cycleBefore).toBeTruthy();

    const movieRequests = { method: 'GET', urlPath: '/api/v3/movie' };
    const baseline = (await mocks.arr.findRequests(movieRequests)).length;

    const slowMovies = arrMoviesStub([{ id: 1, title }]);
    await mocks.arr.stub({
      ...slowMovies,
      priority: 1,
      response: { ...slowMovies.response, fixedDelayMilliseconds: LIBRARY_DELAY_MS },
    });

    await triggerSeeker(api);

    // The library read sits inside the locked pass, so its request means the job holds the lock.
    await expect
      .poll(async () => (await mocks.arr.findRequests(movieRequests)).length, { timeout: SETTLE_MS })
      .toBeGreaterThan(baseline);

    const busy = await api.seeker.resetCycle(instanceId);
    expect(busy.status).toBe(409);
    expect((await busy.json()).detail).toContain('The Seeker is searching this instance');

    await expect
      .poll(async () => (await api.seeker.resetCycle(instanceId)).status, { timeout: SETTLE_MS })
      .toBe(204);

    expect(await currentCycleId(api, instanceId)).not.toBe(cycleBefore);
  });
});
