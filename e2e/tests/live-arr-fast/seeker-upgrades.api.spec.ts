import { mkdirSync, rmSync, truncateSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { test, expect } from '../fixtures/base';
import { indexerMock } from '../helpers/live-arr';
import {
  RADARR,
  arrangeInstance,
  expectNoSearch,
  firstSearchEvent,
  listSearchEvents,
  resetLiveArrState,
  teardownInstances,
  triggerSeeker,
} from '../helpers/seeker-live';
import { torznabSearchStub } from '../helpers/mocks/torznab-stubs';

/**
 * Searching an item that already has a file, because its quality is too low.
 *
 * The file is real: it is written into the mount Radarr scans, and Radarr
 * imports it and parses its quality from the name.
 */

/** Radarr's stock HD-1080p profile, whose cutoff is Bluray-1080p. */
const HD_1080P_PROFILE = 4;

/** Below the profile cutoff, and still an allowed quality so the import succeeds. */
const IMPORTED_FILE = 'F1 (2025) WEBDL-1080p.mkv';

/** Radarr ignores a file small enough to look like a sample. */
const FILE_SIZE_BYTES = 300 * 1024 * 1024;

const MOVIES_DIR = resolve(__dirname, '..', '..', 'test-data', 'radarr-movies');
const SETTLE_MS = 20_000;

interface Movie {
  id: number;
  path: string;
  hasFile: boolean;
  qualityProfileId: number;
  movieFile?: { id: number; qualityCutoffNotMet: boolean };
}

/** Gives the seeded movie a real file that sits below the profile cutoff. */
async function importLowQualityFile(): Promise<() => Promise<void>> {
  const original = await RADARR.arr.get<Movie>(`/api/v3/movie/${RADARR.itemId}`);

  await RADARR.arr.put(`/api/v3/movie/${RADARR.itemId}`, {
    ...original,
    qualityProfileId: HD_1080P_PROFILE,
  });

  const folder = join(MOVIES_DIR, original.path.replace(/^\/movies\/?/, ''));
  const file = join(folder, IMPORTED_FILE);
  mkdirSync(folder, { recursive: true });
  writeFileSync(file, Buffer.alloc(0));
  truncateSync(file, FILE_SIZE_BYTES);

  await RADARR.arr.post('/api/v3/command', { name: 'RescanMovie', movieIds: [RADARR.itemId] });

  await expect
    .poll(async () => (await RADARR.arr.get<Movie>(`/api/v3/movie/${RADARR.itemId}`)).hasFile, { timeout: 90_000 })
    .toBe(true);

  const imported = await RADARR.arr.get<Movie>(`/api/v3/movie/${RADARR.itemId}`);
  expect(imported.movieFile?.qualityCutoffNotMet, 'the imported file should not meet the cutoff').toBe(true);

  return async () => {
    const current = await RADARR.arr.get<Movie>(`/api/v3/movie/${RADARR.itemId}`);
    if (current.movieFile) {
      await RADARR.arr.delete(`/api/v3/moviefile/${current.movieFile.id}`);
    }

    rmSync(folder, { recursive: true, force: true });
    await RADARR.arr.put(`/api/v3/movie/${RADARR.itemId}`, original);
  };
}

test.describe('Seeker quality upgrades', () => {
  test.beforeEach(async () => {
    await resetLiveArrState();
    await indexerMock.stub(torznabSearchStub(RADARR.searchMode, []));
  });

  test.afterEach(async ({ api }) => {
    await teardownInstances(api);
    await resetLiveArrState();
  });

  test('searches a movie whose file is below the quality cutoff', async ({ api }) => {
    test.setTimeout(240_000);

    const restore = await importLowQualityFile();

    try {
      const instanceId = await arrangeInstance(api, RADARR, { instance: { useCutoff: true } });
      await triggerSeeker(api);

      await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(1);

      const event = await firstSearchEvent(api, instanceId);
      expect(event?.itemTitle).toBe(RADARR.seededTitle);
      expect(event?.searchReason).toBe('QualityCutoffNotMet');
    } finally {
      await restore();
    }
  });

  test('leaves a movie with a file alone when the cutoff check is off', async ({ api }) => {
    test.setTimeout(240_000);

    const restore = await importLowQualityFile();

    try {
      const instanceId = await arrangeInstance(api, RADARR, { instance: { useCutoff: false } });
      await expectNoSearch(api, instanceId, SETTLE_MS);
    } finally {
      await restore();
    }
  });
});
