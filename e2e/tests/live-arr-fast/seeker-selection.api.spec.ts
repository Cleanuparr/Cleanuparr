import { test, expect } from '../fixtures/base';
import { indexerMock } from '../helpers/live-arr';
import {
  RADARR,
  arrangeInstance,
  firstSearchEvent,
  listSearchEvents,
  resetLiveArrState,
  restoreLibrary,
  snapshotLibrary,
  teardownInstances,
  triggerSeeker,
} from '../helpers/seeker-live';
import { torznabSearchStub } from '../helpers/mocks/torznab-stubs';

/**
 * Which candidate a strategy picks when the library has more than one.
 *
 * Only the deterministic strategies are here.
 * The weighted and random ones have no assertion that is not a coin flip.
 *
 * The seed leaves the second movie unmonitored, so every other spec sees one
 * candidate. These tests monitor it and the teardown puts the library back.
 */

const SETTLE_MS = 20_000;

interface Candidate {
  id: number;
  title: string;
  added: string;
  lastSearchTime: string | null;
}

async function monitorEveryMovie(): Promise<Candidate[]> {
  const movies = await RADARR.arr.get<Array<Record<string, unknown>>>('/api/v3/movie');

  for (const movie of movies) {
    if (!movie.monitored) {
      await RADARR.arr.put(`/api/v3/movie/${movie.id}`, { ...movie, monitored: true });
    }
  }

  return movies.map((movie) => ({
    id: movie.id as number,
    title: movie.title as string,
    added: movie.added as string,
    lastSearchTime: (movie.lastSearchTime as string | undefined) ?? null,
  }));
}

function searchedAt(lastSearchTime: string | null | undefined): number {
  return lastSearchTime ? Date.parse(lastSearchTime) : 0;
}

/** Every title tied for the oldest search time. */
function oldestSearched(candidates: Candidate[]): string[] {
  const oldest = Math.min(...candidates.map((c) => searchedAt(c.lastSearchTime)));
  return candidates.filter((c) => searchedAt(c.lastSearchTime) === oldest).map((c) => c.title);
}

test.describe('Seeker selection strategies', () => {
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

  test('newest first picks the most recently added movie', async ({ api }) => {
    test.setTimeout(180_000);

    const candidates = await monitorEveryMovie();
    expect(candidates.length).toBeGreaterThan(1);

    const newest = [...candidates].sort((a, b) => b.added.localeCompare(a.added))[0];

    const instanceId = await arrangeInstance(api, RADARR, { config: { selectionStrategy: 'NewestFirst' } });
    await triggerSeeker(api);

    await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(1);

    const event = await firstSearchEvent(api, instanceId);
    expect(event?.itemTitle).toBe(newest.title);
  });

  test("oldest search first follows the arr's last search time", async ({ api }) => {
    test.setTimeout(180_000);

    const candidates = await monitorEveryMovie();
    expect(candidates.length).toBeGreaterThan(1);

    const strategy = { config: { selectionStrategy: 'OldestSearchFirst' } };

    const firstRun = await arrangeInstance(api, RADARR, strategy);
    await triggerSeeker(api);
    await expect.poll(async () => (await listSearchEvents(api, firstRun)).length, { timeout: SETTLE_MS }).toBe(1);
    const firstChoice = (await firstSearchEvent(api, firstRun))?.itemTitle;
    expect(oldestSearched(candidates)).toContain(firstChoice);

    const picked = candidates.find((c) => c.title === firstChoice)!;
    await expect
      .poll(
        async () => searchedAt((await RADARR.arr.get<Record<string, unknown>>(`/api/v3/movie/${picked.id}`)).lastSearchTime as string),
        { timeout: SETTLE_MS },
      )
      .toBeGreaterThan(searchedAt(picked.lastSearchTime));

    // A fresh instance has no Seeker history, so only the arr's search times order the movies.
    await teardownInstances(api);
    const searchedOnce = await monitorEveryMovie();

    const secondRun = await arrangeInstance(api, RADARR, strategy);
    await triggerSeeker(api);
    await expect.poll(async () => (await listSearchEvents(api, secondRun)).length, { timeout: SETTLE_MS }).toBe(1);
    const secondChoice = (await firstSearchEvent(api, secondRun))?.itemTitle;

    expect(oldestSearched(searchedOnce)).toContain(secondChoice);
    expect(secondChoice).not.toBe(firstChoice);
  });

  test('a cycle covers every candidate before repeating one', async ({ api }) => {
    test.setTimeout(180_000);

    const candidates = await monitorEveryMovie();

    const instanceId = await arrangeInstance(api, RADARR, { instance: { minCycleTimeDays: 0 } });

    for (let run = 1; run <= candidates.length; run++) {
      await triggerSeeker(api);
      await expect
        .poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS })
        .toBe(run);
    }

    const searched = (await listSearchEvents(api, instanceId)).map((event) => event.itemTitle);
    expect(new Set(searched).size).toBe(candidates.length);
  });
});
