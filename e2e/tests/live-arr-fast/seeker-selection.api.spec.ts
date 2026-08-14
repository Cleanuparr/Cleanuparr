import { test, expect } from '../fixtures/base';
import { indexerMock } from '../helpers/live-arr';
import {
  RADARR,
  arrangeInstance,
  firstSearchEvent,
  listSearchEvents,
  resetLiveArrState,
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
 * candidate. These tests monitor it and put it back afterwards.
 */

const SETTLE_MS = 20_000;

interface Candidate {
  id: number;
  title: string;
  added: string;
}

async function monitoredLibrary(): Promise<{ candidates: Candidate[]; restore: () => Promise<void> }> {
  const movies = await RADARR.arr.get<Array<Record<string, unknown>>>('/api/v3/movie');
  const originals = movies.map((movie) => ({ ...movie }));

  for (const movie of movies) {
    if (!movie.monitored) {
      await RADARR.arr.put(`/api/v3/movie/${movie.id}`, { ...movie, monitored: true });
    }
  }

  return {
    candidates: movies.map((movie) => ({
      id: movie.id as number,
      title: movie.title as string,
      added: movie.added as string,
    })),
    restore: async () => {
      for (const movie of originals) {
        await RADARR.arr.put(`/api/v3/movie/${movie.id}`, movie);
      }
    },
  };
}

test.describe('Seeker selection strategies', () => {
  test.beforeEach(async () => {
    await resetLiveArrState();
    await indexerMock.stub(torznabSearchStub(RADARR.searchMode, []));
  });

  test.afterEach(async ({ api }) => {
    await teardownInstances(api);
    await resetLiveArrState();
  });

  test('newest first picks the most recently added movie', async ({ api }) => {
    test.setTimeout(180_000);

    const library = await monitoredLibrary();
    expect(library.candidates.length).toBeGreaterThan(1);

    const newest = [...library.candidates].sort((a, b) => b.added.localeCompare(a.added))[0];

    try {
      const instanceId = await arrangeInstance(api, RADARR, { config: { selectionStrategy: 'NewestFirst' } });
      await triggerSeeker(api);

      await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(1);

      const event = await firstSearchEvent(api, instanceId);
      expect(event?.itemTitle).toBe(newest.title);
    } finally {
      await library.restore();
    }
  });

  test('oldest search first makes the same choice every time', async ({ api }) => {
    test.setTimeout(180_000);

    const library = await monitoredLibrary();
    expect(library.candidates.length).toBeGreaterThan(1);

    const strategy = { config: { selectionStrategy: 'OldestSearchFirst' } };

    try {
      const firstRun = await arrangeInstance(api, RADARR, strategy);
      await triggerSeeker(api);
      await expect.poll(async () => (await listSearchEvents(api, firstRun)).length, { timeout: SETTLE_MS }).toBe(1);
      const firstChoice = (await firstSearchEvent(api, firstRun))?.itemTitle;

      // A fresh instance starts with no search history, so the choice is made from scratch.
      await teardownInstances(api);

      const secondRun = await arrangeInstance(api, RADARR, strategy);
      await triggerSeeker(api);
      await expect.poll(async () => (await listSearchEvents(api, secondRun)).length, { timeout: SETTLE_MS }).toBe(1);

      expect((await firstSearchEvent(api, secondRun))?.itemTitle).toBe(firstChoice);
    } finally {
      await library.restore();
    }
  });

  test('a cycle covers every candidate before repeating one', async ({ api }) => {
    test.setTimeout(180_000);

    const library = await monitoredLibrary();

    try {
      const instanceId = await arrangeInstance(api, RADARR, { instance: { minCycleTimeDays: 0 } });

      for (let run = 1; run <= library.candidates.length; run++) {
        await triggerSeeker(api);
        await expect
          .poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS })
          .toBe(run);
      }

      const searched = (await listSearchEvents(api, instanceId)).map((event) => event.itemTitle);
      expect(new Set(searched).size).toBe(library.candidates.length);
    } finally {
      await library.restore();
    }
  });
});
