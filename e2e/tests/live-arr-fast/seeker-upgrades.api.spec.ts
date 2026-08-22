import { rmSync, truncateSync } from 'node:fs';
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
  restoreLibrary,
  snapshotLibrary,
  teardownInstances,
  triggerSeeker,
} from '../helpers/seeker-live';
import { torznabSearchStub } from '../helpers/mocks/torznab-stubs';
import { mkdirShared, writeFileShared } from '../helpers/shared-volume';

/**
 * Searching an item that already has a file, because its quality is too low.
 *
 * The file is real: it is written into the mount Radarr scans, and Radarr
 * imports it and parses its quality from the name.
 */

/** Radarr's stock HD-1080p profile, whose cutoff is Bluray-1080p. */
const HD_1080P_PROFILE = 4;

/** Allowed by that profile and below its cutoff, so the import counts as upgradable. */
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

function movieFolder(movie: Movie): string {
  return join(MOVIES_DIR, movie.path.replace(/^\/movies\/?/, ''));
}

/**
 * Gives the seeded movie a real file that sits below the profile cutoff.
 *
 * The stock profiles ship with upgrades turned off, and Radarr reports the
 * cutoff as met whenever it will not upgrade, so this turns them on.
 */
async function importLowQualityFile(): Promise<void> {
  const movie = await RADARR.arr.get<Movie>(`/api/v3/movie/${RADARR.itemId}`);
  const profile = await RADARR.arr.get<Record<string, unknown>>(`/api/v3/qualityprofile/${HD_1080P_PROFILE}`);

  await RADARR.arr.put(`/api/v3/qualityprofile/${HD_1080P_PROFILE}`, { ...profile, upgradeAllowed: true });
  await RADARR.arr.put(`/api/v3/movie/${RADARR.itemId}`, { ...movie, qualityProfileId: HD_1080P_PROFILE });

  const folder = movieFolder(movie);
  mkdirShared(folder);
  const file = join(folder, IMPORTED_FILE);
  writeFileShared(file, Buffer.alloc(0));
  truncateSync(file, FILE_SIZE_BYTES);

  await RADARR.arr.post('/api/v3/command', { name: 'RescanMovie', movieIds: [RADARR.itemId] });

  await expect
    .poll(async () => (await RADARR.arr.get<Movie>(`/api/v3/movie/${RADARR.itemId}`)).hasFile, { timeout: 90_000 })
    .toBe(true);

  const imported = await RADARR.arr.get<Movie>(`/api/v3/movie/${RADARR.itemId}`);
  expect(imported.movieFile?.qualityCutoffNotMet, 'the imported file should not meet the cutoff').toBe(true);
}

/** Runs even when no test imported anything, because a half-done import still leaves a file. */
async function dropImportedFile(): Promise<void> {
  const movie = await RADARR.arr.get<Movie>(`/api/v3/movie/${RADARR.itemId}`);

  if (movie.movieFile) {
    await RADARR.arr.delete(`/api/v3/moviefile/${movie.movieFile.id}`);
  }

  rmSync(movieFolder(movie), { recursive: true, force: true });
}

test.describe('Seeker quality upgrades', () => {
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
    await dropImportedFile();
    await RADARR.arr.put(`/api/v3/qualityprofile/${HD_1080P_PROFILE}`, profile);
    await restoreLibrary(RADARR, library);
    await resetLiveArrState();
  });

  test('searches a movie whose file is below the quality cutoff', async ({ api }) => {
    test.setTimeout(240_000);

    await importLowQualityFile();

    const instanceId = await arrangeInstance(api, RADARR, { instance: { useCutoff: true } });
    await triggerSeeker(api);

    await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(1);

    const event = await firstSearchEvent(api, instanceId);
    expect(event?.itemTitle).toBe(RADARR.seededTitle);
    expect(event?.searchReason).toBe('QualityCutoffNotMet');
  });

  test('leaves a movie with a file alone when the cutoff check is off', async ({ api }) => {
    test.setTimeout(240_000);

    await importLowQualityFile();

    const instanceId = await arrangeInstance(api, RADARR, { instance: { useCutoff: false } });
    await expectNoSearch(api, instanceId, SETTLE_MS);
  });
});
