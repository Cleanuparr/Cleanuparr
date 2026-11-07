import { rmSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { test, expect } from '../fixtures/base';
import type { CleanuparrApi } from '../helpers/api';
import { indexerMock } from '../helpers/live-arr';
import {
  HD_1080P_PROFILE,
  RADARR,
  SONARR,
  type ProfileChanges,
  arrangeInstance,
  dropImportedMovie,
  expectNoSearch,
  firstSearchEvent,
  importLowQualityMovie,
  listSearchEvents,
  resetLiveArrState,
  restoreLibrary,
  snapshotLibrary,
  teardownInstances,
  triggerSeeker,
  writeFakeVideo,
} from '../helpers/seeker-live';
import { torznabSearchStub } from '../helpers/mocks/torznab-stubs';

/**
 * Searching an item that already has a file, because its quality or custom format score is too low.
 *
 * The file is real: it is written into the mount the arr scans, and the arr
 * imports it and parses its quality from the name.
 */

/** S01E01's id equals the series id, which hides id mix-ups, so this uses S01E02. */
const IMPORTED_EPISODE = { seasonNumber: 1, episodeNumber: 2 };
const IMPORTED_EPISODE_FILE = 'Agatha All Along - S01E02 - WEBDL-1080p.mkv';

/** No custom format exists, so every file scores 0 and sits below this cutoff. */
const CUSTOM_FORMAT_CUTOFF = 100;

const TV_DIR = resolve(__dirname, '..', '..', 'test-data', 'sonarr-tv');
const SETTLE_MS = 20_000;

interface Series {
  id: number;
  path: string;
}

interface Episode {
  id: number;
  seasonNumber: number;
  episodeNumber: number;
  hasFile: boolean;
  episodeFileId: number;
  monitored: boolean;
}

function seriesFolder(series: Series): string {
  return join(TV_DIR, series.path.replace(/^\/tv\/?/, ''));
}

async function listEpisodes(): Promise<Episode[]> {
  return SONARR.arr.get<Episode[]>(`/api/v3/episode?seriesId=${SONARR.itemId}`);
}

/**
 * Gives one seeded episode a real file and unmonitors the rest.
 *
 * The other episodes are missing, so a monitored one would make the season search for that reason instead.
 */
async function importLowQualityEpisode(profileChanges: ProfileChanges): Promise<void> {
  const series = await SONARR.arr.get<Record<string, unknown> & Series>(`/api/v3/series/${SONARR.itemId}`);
  const profile = await SONARR.arr.get<Record<string, unknown>>(`/api/v3/qualityprofile/${HD_1080P_PROFILE}`);

  await SONARR.arr.put(`/api/v3/qualityprofile/${HD_1080P_PROFILE}`, { ...profile, ...profileChanges });
  await SONARR.arr.put(`/api/v3/series/${SONARR.itemId}`, { ...series, qualityProfileId: HD_1080P_PROFILE });

  const episodes = await listEpisodes();
  const others = episodes.filter(
    (e) => e.seasonNumber !== IMPORTED_EPISODE.seasonNumber || e.episodeNumber !== IMPORTED_EPISODE.episodeNumber,
  );
  await SONARR.arr.put('/api/v3/episode/monitor', { episodeIds: others.map((e) => e.id), monitored: false });

  writeFakeVideo(join(seriesFolder(series), 'Season 01'), IMPORTED_EPISODE_FILE);

  await SONARR.arr.post('/api/v3/command', { name: 'RescanSeries', seriesId: SONARR.itemId });

  await expect
    .poll(
      async () =>
        (await listEpisodes()).some(
          (e) =>
            e.hasFile &&
            e.seasonNumber === IMPORTED_EPISODE.seasonNumber &&
            e.episodeNumber === IMPORTED_EPISODE.episodeNumber,
        ),
      { timeout: 90_000 },
    )
    .toBe(true);
}

/** Restores monitoring from the snapshot, because the seed leaves the specials unmonitored. */
async function dropImportedEpisode(snapshot: Episode[]): Promise<void> {
  const series = await SONARR.arr.get<Series>(`/api/v3/series/${SONARR.itemId}`);
  const imported = (await listEpisodes()).find(
    (e) => e.seasonNumber === IMPORTED_EPISODE.seasonNumber && e.episodeNumber === IMPORTED_EPISODE.episodeNumber,
  );

  if (imported?.hasFile) {
    await SONARR.arr.delete(`/api/v3/episodefile/${imported.episodeFileId}`);
  }

  for (const monitored of [true, false]) {
    const episodeIds = snapshot.filter((e) => e.monitored === monitored).map((e) => e.id);
    if (episodeIds.length > 0) {
      await SONARR.arr.put('/api/v3/episode/monitor', { episodeIds, monitored });
    }
  }

  rmSync(join(seriesFolder(series), 'Season 01'), { recursive: true, force: true });
}

/** Caches the file's custom format score, which the Seeker reads instead of asking the arr. */
async function syncCustomFormatScores(api: CleanuparrApi, instanceId: string): Promise<void> {
  const res = await api.jobs.trigger('CustomFormatScoreSyncer');
  expect(res.ok, `triggering the custom format score sync failed with ${res.status}`).toBe(true);

  await expect
    .poll(
      async () => {
        const body = await (await api.seeker.listCustomFormatScores({ instanceId })).json();
        return (body.items as Array<{ isBelowCutoff: boolean }>).some((e) => e.isBelowCutoff);
      },
      { timeout: 60_000 },
    )
    .toBe(true);
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
    await dropImportedMovie();
    await RADARR.arr.put(`/api/v3/qualityprofile/${HD_1080P_PROFILE}`, profile);
    await restoreLibrary(RADARR, library);
    await resetLiveArrState();
  });

  test('searches a movie whose file is below the quality cutoff', async ({ api }) => {
    test.setTimeout(240_000);

    // Radarr reports the cutoff as met whenever it will not upgrade.
    const imported = await importLowQualityMovie({ upgradeAllowed: true });
    expect(imported.movieFile?.qualityCutoffNotMet, 'the imported file should not meet the cutoff').toBe(true);

    const instanceId = await arrangeInstance(api, RADARR, { instance: { useCutoff: true } });
    await triggerSeeker(api);

    await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(1);

    const event = await firstSearchEvent(api, instanceId);
    expect(event?.itemTitle).toBe(RADARR.seededTitle);
    expect(event?.searchReason).toBe('QualityCutoffNotMet');
  });

  test('leaves a movie with a file alone when the cutoff check is off', async ({ api }) => {
    test.setTimeout(240_000);

    await importLowQualityMovie({ upgradeAllowed: true });

    const instanceId = await arrangeInstance(api, RADARR, { instance: { useCutoff: false } });
    await expectNoSearch(api, instanceId, SETTLE_MS);
  });

  test('searches a movie below the custom format cutoff', async ({ api }) => {
    test.setTimeout(240_000);

    await importLowQualityMovie({ upgradeAllowed: true, cutoffFormatScore: CUSTOM_FORMAT_CUTOFF });

    const instanceId = await arrangeInstance(api, RADARR, {
      instance: { useCutoff: false, useCustomFormatScore: true },
    });
    await syncCustomFormatScores(api, instanceId);
    await triggerSeeker(api);

    await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(1);

    const event = await firstSearchEvent(api, instanceId);
    expect(event?.itemTitle).toBe(RADARR.seededTitle);
    expect(event?.searchReason).toBe('CustomFormatScoreBelowCutoff');
  });

  test('leaves a movie below the custom format cutoff alone when its profile disallows upgrades', async ({ api }) => {
    test.setTimeout(240_000);

    await importLowQualityMovie({ upgradeAllowed: false, cutoffFormatScore: CUSTOM_FORMAT_CUTOFF });

    const instanceId = await arrangeInstance(api, RADARR, {
      instance: { useCutoff: false, useCustomFormatScore: true },
    });
    await syncCustomFormatScores(api, instanceId);
    await expectNoSearch(api, instanceId, SETTLE_MS);
  });
});

test.describe('Seeker custom format upgrades on Sonarr', () => {
  let library: Array<Record<string, unknown>> = [];
  let episodes: Episode[] = [];
  let profile: Record<string, unknown> = {};

  test.beforeEach(async () => {
    await resetLiveArrState();
    library = await snapshotLibrary(SONARR);
    episodes = await listEpisodes();
    profile = await SONARR.arr.get<Record<string, unknown>>(`/api/v3/qualityprofile/${HD_1080P_PROFILE}`);
    await indexerMock.stub(torznabSearchStub(SONARR.searchMode, []));
  });

  test.afterEach(async ({ api }) => {
    await teardownInstances(api);
    await dropImportedEpisode(episodes);
    await SONARR.arr.put(`/api/v3/qualityprofile/${HD_1080P_PROFILE}`, profile);
    await restoreLibrary(SONARR, library);
    await resetLiveArrState();
  });

  test('searches a season whose episode is below the custom format cutoff', async ({ api }) => {
    test.setTimeout(240_000);

    await importLowQualityEpisode({ upgradeAllowed: true, cutoffFormatScore: CUSTOM_FORMAT_CUTOFF });

    const instanceId = await arrangeInstance(api, SONARR, {
      instance: { useCutoff: false, useCustomFormatScore: true },
    });
    await syncCustomFormatScores(api, instanceId);
    await triggerSeeker(api);

    await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(1);

    const event = await firstSearchEvent(api, instanceId);
    expect(event?.itemTitle).toBe(`${SONARR.seededTitle} S01`);
    expect(event?.searchReason).toBe('CustomFormatScoreBelowCutoff');
  });

  test('leaves an episode below the custom format cutoff alone when its profile disallows upgrades', async ({ api }) => {
    test.setTimeout(240_000);

    await importLowQualityEpisode({ upgradeAllowed: false, cutoffFormatScore: CUSTOM_FORMAT_CUTOFF });

    const instanceId = await arrangeInstance(api, SONARR, {
      instance: { useCutoff: false, useCustomFormatScore: true },
    });
    await syncCustomFormatScores(api, instanceId);
    await expectNoSearch(api, instanceId, SETTLE_MS);
  });
});
