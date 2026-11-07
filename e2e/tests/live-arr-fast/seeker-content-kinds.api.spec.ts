import { test, expect } from '../fixtures/base';
import { indexerMock, type LiveArrCommand } from '../helpers/live-arr';
import {
  SONARR,
  arrangeInstance,
  listSearchEvents,
  resetLiveArrState,
  restoreLibrary,
  snapshotLibrary,
  teardownInstances,
  triggerSeeker,
} from '../helpers/seeker-live';
import { torznabSearchStub } from '../helpers/mocks/torznab-stubs';

/**
 * How the Seeker shapes a Sonarr search for each content kind.
 *
 * Sonarr runs one indexer query per episode for a specials season and for an airing anime season.
 * The Seeker searches those one episode at a time.
 */

const SETTLE_MS = 20_000;

/** Sherlock, added unmonitored by the seed for its aired specials. */
const SPECIALS_SERIES_ID = 2;
/** "Unlocking Sherlock", aired 2010. */
const SPECIAL_EPISODE_NUMBER = 2;

interface SonarrEpisode {
  id: number;
  seasonNumber: number;
  episodeNumber: number;
}

async function searchCommandsAfter(lastId: number): Promise<LiveArrCommand[]> {
  const commands = await SONARR.arr.commands();
  return commands.filter((c) => c.id > lastId && (c.name === 'EpisodeSearch' || c.name === 'SeasonSearch'));
}

async function lastCommandId(): Promise<number> {
  const commands = await SONARR.arr.commands();
  return Math.max(0, ...commands.map((c) => c.id));
}

/**
 * Puts the seeded series type back once Sonarr's own commands finish.
 * A refresh still running after the type change would save the old type over the restore.
 */
async function restoreSeriesType(seeded: Record<string, unknown>): Promise<void> {
  await expect
    .poll(async () => (await SONARR.arr.commands()).filter((c) => c.status === 'queued' || c.status === 'started').length, {
      timeout: SETTLE_MS,
    })
    .toBe(0);

  await SONARR.arr.put(`/api/v3/series/${seeded.id}`, seeded);

  const restored = await SONARR.arr.get<{ seriesType: string }>(`/api/v3/series/${seeded.id}`);
  expect(restored.seriesType).toBe(seeded.seriesType);
}

test.describe('Seeker Sonarr content kinds', () => {
  let library: Array<Record<string, unknown>>;

  test.beforeEach(async () => {
    await resetLiveArrState();
    await indexerMock.stub(torznabSearchStub(SONARR.searchMode, []));
    library = await snapshotLibrary(SONARR);
  });

  test.afterEach(async ({ api }) => {
    await teardownInstances(api);
    await restoreLibrary(SONARR, library);
    await resetLiveArrState();
  });

  test('searches a special by episode, never by season', async ({ api }) => {
    test.setTimeout(180_000);

    const episodes = await SONARR.arr.get<SonarrEpisode[]>(`/api/v3/episode?seriesId=${SPECIALS_SERIES_ID}`);
    const special = episodes.find((e) => e.seasonNumber === 0 && e.episodeNumber === SPECIAL_EPISODE_NUMBER);
    expect(special, 'the seed lost the Sherlock special').toBeTruthy();

    for (const series of library) {
      await SONARR.arr.put(`/api/v3/series/${series.id}`, { ...series, monitored: series.id === SPECIALS_SERIES_ID });
    }
    await SONARR.arr.put('/api/v3/episode/monitor', { episodeIds: [special!.id], monitored: true });

    try {
      const instanceId = await arrangeInstance(api, SONARR);
      const before = await lastCommandId();

      await triggerSeeker(api);
      await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(1);

      const [event] = await listSearchEvents(api, instanceId);
      expect(event.contentKind).toBe('Special');
      expect(event.itemTitle).toBe('Sherlock S00E02');

      await expect.poll(async () => (await searchCommandsAfter(before)).length, { timeout: SETTLE_MS }).toBe(1);
      const [command] = await searchCommandsAfter(before);
      expect(command.name).toBe('EpisodeSearch');
      expect(command.body?.episodeIds).toEqual([special!.id]);
    } finally {
      await SONARR.arr.put('/api/v3/episode/monitor', { episodeIds: [special!.id], monitored: false });
    }
  });

  test('searches a fully aired anime season as a whole', async ({ api }) => {
    test.setTimeout(180_000);

    const seeded = library.find((s) => s.id === SONARR.itemId)!;
    await SONARR.arr.put(`/api/v3/series/${seeded.id}`, { ...seeded, seriesType: 'anime' });

    try {
      const instanceId = await arrangeInstance(api, SONARR);
      const before = await lastCommandId();

      await triggerSeeker(api);
      await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(1);

      const [event] = await listSearchEvents(api, instanceId);
      expect(event.contentKind).toBe('Anime');

      await expect.poll(async () => (await searchCommandsAfter(before)).length, { timeout: SETTLE_MS }).toBe(1);
      const [command] = await searchCommandsAfter(before);
      expect(command.name).toBe('SeasonSearch');
      expect(command.body?.seasonNumber).toBe(1);
    } finally {
      await restoreSeriesType(seeded);
    }
  });

  test('tags a regular season search as standard', async ({ api }) => {
    test.setTimeout(180_000);

    const instanceId = await arrangeInstance(api, SONARR);
    const before = await lastCommandId();

    await triggerSeeker(api);
    await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(1);

    const [event] = await listSearchEvents(api, instanceId);
    expect(event.contentKind).toBe('Standard');

    await expect.poll(async () => (await searchCommandsAfter(before)).length, { timeout: SETTLE_MS }).toBe(1);
    expect((await searchCommandsAfter(before))[0].name).toBe('SeasonSearch');
  });
});
