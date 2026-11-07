import { test, expect } from '../fixtures/base';
import type { CleanuparrApi } from '../helpers/api';
import { indexerMock } from '../helpers/live-arr';
import {
  HD_1080P_PROFILE,
  RADARR,
  arrangeInstance,
  dropImportedMovie,
  firstSearchEvent,
  importLowQualityMovie,
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
 * The deterministic strategies get exact picks.
 * Custom Format Gap Weighted gets skewed odds over repeated previews.
 * The other weighted strategies and Random have no assertion that is not a coin flip.
 *
 * The seed leaves the second movie unmonitored, so every other spec sees one
 * candidate. These tests monitor it and the teardown puts the library back.
 */

const SETTLE_MS = 20_000;

/** At 80% odds per draw, 24 or more leads fails about once in a thousand runs. */
const PREVIEW_DRAWS = 40;
const MIN_MISSING_LEADS = 24;

interface Preview {
  randomPick: boolean;
  nextUp: Array<{ name: string }>;
}

async function getPreview(api: CleanuparrApi, instanceId: string): Promise<Preview> {
  const res = await api.seeker.getPreview(instanceId);
  expect(res.status, await res.clone().text()).toBe(200);
  return (await res.json()) as Preview;
}

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
  let profile: Record<string, unknown> = {};

  test.beforeEach(async () => {
    await resetLiveArrState();
    library = await snapshotLibrary(RADARR);
    profile = await RADARR.arr.get<Record<string, unknown>>(`/api/v3/qualityprofile/${HD_1080P_PROFILE}`);
    await indexerMock.stub(torznabSearchStub(RADARR.searchMode, []));
  });

  test.afterEach(async ({ api }) => {
    await teardownInstances(api);
    await dropImportedMovie();
    await RADARR.arr.put(`/api/v3/qualityprofile/${HD_1080P_PROFILE}`, profile);
    await restoreLibrary(RADARR, library);
    await resetLiveArrState();
  });

  test('newest first picks the most recently added movie', async ({ api }) => {
    test.setTimeout(180_000);

    const candidates = await monitorEveryMovie();
    expect(candidates.length).toBeGreaterThan(1);

    // The seed adds both movies in the same second, and Radarr ignores edits to the date.
    const newestAdded = Math.max(...candidates.map((c) => Date.parse(c.added)));
    const newest = candidates.filter((c) => Date.parse(c.added) === newestAdded).map((c) => c.title);

    const instanceId = await arrangeInstance(api, RADARR, { config: { selectionStrategy: 'NewestFirst' } });
    await triggerSeeker(api);

    await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(1);

    const event = await firstSearchEvent(api, instanceId);
    expect(newest).toContain(event?.itemTitle);
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

  test('custom format gap weighted favors the missing movie over a zero-gap upgrade', async ({ api }) => {
    test.setTimeout(240_000);

    const candidates = await monitorEveryMovie();
    expect(candidates).toHaveLength(2);
    const missing = candidates.find((c) => c.id !== RADARR.itemId)!;

    // A WEBDL-1080p file misses the quality cutoff but meets a custom format cutoff of 0, so its gap is 0.
    await importLowQualityMovie({ upgradeAllowed: true, cutoffFormatScore: 0 });

    // With the newer search on the upgrade, the weights are 1.0 for the missing movie and 0.25 for the upgrade.
    const before = await RADARR.arr.get<Record<string, unknown>>(`/api/v3/movie/${RADARR.itemId}`);
    await RADARR.arr.post('/api/v3/command', { name: 'MoviesSearch', movieIds: [RADARR.itemId] });
    await expect
      .poll(
        async () =>
          searchedAt((await RADARR.arr.get<Record<string, unknown>>(`/api/v3/movie/${RADARR.itemId}`)).lastSearchTime as string),
        { timeout: SETTLE_MS },
      )
      .toBeGreaterThan(searchedAt(before.lastSearchTime as string | undefined));

    const instanceId = await arrangeInstance(api, RADARR, {
      config: { selectionStrategy: 'CustomFormatGapWeighted' },
      instance: { useCutoff: true, useCustomFormatScore: true },
    });

    let missingLeads = 0;
    for (let draw = 0; draw < PREVIEW_DRAWS; draw++) {
      const preview = await getPreview(api, instanceId);
      expect(preview.nextUp.map((u) => u.name).sort()).toEqual([missing.title, RADARR.seededTitle].sort());
      if (preview.nextUp[0].name === missing.title) {
        missingLeads++;
      }
    }

    expect(missingLeads).toBeGreaterThanOrEqual(MIN_MISSING_LEADS);

    await triggerSeeker(api);
    await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(1);
  });

  test('custom format gap weighted falls back to balanced without custom format scores', async ({ api }) => {
    const candidates = await monitorEveryMovie();

    const instanceId = await arrangeInstance(api, RADARR, {
      config: { selectionStrategy: 'CustomFormatGapWeighted' },
      instance: { useCustomFormatScore: false },
    });

    const preview = await getPreview(api, instanceId);

    expect(preview.randomPick).toBe(true);
    expect(preview.nextUp.map((u) => u.name).sort()).toEqual(candidates.map((c) => c.title).sort());
  });
});
