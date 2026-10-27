import { rmSync, truncateSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { test, expect, TEST_CONFIG } from '../fixtures/base';
import type { CleanuparrApi } from '../helpers/api';
import { indexerMock, type LiveArrCommand } from '../helpers/live-arr';
import {
  SONARR,
  WHISPARR2,
  type SeededArr,
  arrangeInstance,
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
 * How the Sonarr search mode picks between a season search and an episode search.
 *
 * The seeded season has nine aired monitored episodes and no files.
 */

const SETTLE_MS = 20_000;

/** A file on S01E01 leaves the season partly wanted. */
const IMPORTED_EPISODE_FILE = 'Agatha All Along - S01E01 - WEBDL-1080p.mkv';

/** The arr ignores a file small enough to look like a sample. */
const FILE_SIZE_BYTES = 300 * 1024 * 1024;

const TV_DIR = resolve(__dirname, '..', '..', 'test-data', 'sonarr-tv');

interface Episode {
  id: number;
  seasonNumber: number;
  episodeNumber: number;
  hasFile: boolean;
  episodeFileId: number;
  monitored: boolean;
}

async function searchCommandsAfter(lastId: number, target: SeededArr = SONARR): Promise<LiveArrCommand[]> {
  const commands = await target.arr.commands();
  return commands.filter((c) => c.id > lastId && (c.name === 'EpisodeSearch' || c.name === 'SeasonSearch'));
}

async function lastCommandId(target: SeededArr = SONARR): Promise<number> {
  const commands = await target.arr.commands();
  return Math.max(0, ...commands.map((c) => c.id));
}

async function listEpisodes(): Promise<Episode[]> {
  return SONARR.arr.get<Episode[]>(`/api/v3/episode?seriesId=${SONARR.itemId}`);
}

function findEpisode(episodes: Episode[], episodeNumber: number): Episode {
  const episode = episodes.find((e) => e.seasonNumber === 1 && e.episodeNumber === episodeNumber);
  expect(episode, `the seed lost S01E${episodeNumber}`).toBeTruthy();
  return episode!;
}

async function seasonFolder(): Promise<string> {
  const series = await SONARR.arr.get<{ path: string }>(`/api/v3/series/${SONARR.itemId}`);
  return join(TV_DIR, series.path.replace(/^\/tv\/?/, ''), 'Season 01');
}

/** Gives S01E01 a real file, so the arr counts it in the season but no longer wants it. */
async function importFirstEpisode(): Promise<void> {
  const folder = await seasonFolder();
  mkdirShared(folder);
  const file = join(folder, IMPORTED_EPISODE_FILE);
  writeFileShared(file, Buffer.alloc(0));
  truncateSync(file, FILE_SIZE_BYTES);

  await SONARR.arr.post('/api/v3/command', { name: 'RescanSeries', seriesId: SONARR.itemId });

  await expect
    .poll(async () => findEpisode(await listEpisodes(), 1).hasFile, { timeout: 90_000 })
    .toBe(true);
}

/** Runs even when no test imported anything, because a half-done import still leaves a file. */
async function dropImportedEpisode(): Promise<void> {
  const first = findEpisode(await listEpisodes(), 1);

  if (first.hasFile) {
    await SONARR.arr.delete(`/api/v3/episodefile/${first.episodeFileId}`);
  }

  rmSync(await seasonFolder(), { recursive: true, force: true });
}

/** Sonarr hands back an identical SeasonSearch still running instead of queueing a new one. */
async function waitForIdleSonarr(): Promise<void> {
  await expect
    .poll(async () => (await SONARR.arr.commands()).filter((c) => c.status === 'queued' || c.status === 'started').length, {
      timeout: 60_000,
    })
    .toBe(0);
}

/** Runs the Seeker once and returns the one search command it sent. */
async function searchOnce(
  api: CleanuparrApi,
  instanceId: string,
  target: SeededArr = SONARR,
): Promise<LiveArrCommand> {
  const before = await lastCommandId(target);

  await triggerSeeker(api);
  await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(1);

  await expect.poll(async () => (await searchCommandsAfter(before, target)).length, { timeout: SETTLE_MS }).toBe(1);
  return (await searchCommandsAfter(before, target))[0];
}

test.describe('Seeker Sonarr search mode', () => {
  let library: Array<Record<string, unknown>>;
  let episodes: Episode[];

  test.beforeEach(async () => {
    await waitForIdleSonarr();
    await resetLiveArrState();
    await indexerMock.stub(torznabSearchStub(SONARR.searchMode, []));
    library = await snapshotLibrary(SONARR);
    episodes = await listEpisodes();
  });

  test.afterEach(async ({ api }) => {
    await teardownInstances(api);
    await dropImportedEpisode();
    for (const monitored of [true, false]) {
      const episodeIds = episodes.filter((e) => e.monitored === monitored).map((e) => e.id);
      if (episodeIds.length > 0) {
        await SONARR.arr.put('/api/v3/episode/monitor', { episodeIds, monitored });
      }
    }
    await restoreLibrary(SONARR, library);
    await resetLiveArrState();
  });

  test('auto searches a partly wanted season one episode at a time', async ({ api }) => {
    test.setTimeout(240_000);
    await importFirstEpisode();

    const instanceId = await arrangeInstance(api, SONARR, { instance: { sonarrSearchMode: 'Auto' } });
    const command = await searchOnce(api, instanceId);

    expect(command.name).toBe('EpisodeSearch');
    expect(command.body?.episodeIds).toEqual([findEpisode(episodes, 2).id]);
  });

  test('auto searches a fully wanted aired season as a whole', async ({ api }) => {
    test.setTimeout(180_000);

    const instanceId = await arrangeInstance(api, SONARR, { instance: { sonarrSearchMode: 'Auto' } });
    const command = await searchOnce(api, instanceId);

    expect(command.name).toBe('SeasonSearch');
    expect(command.body?.seasonNumber).toBe(1);
  });

  test('season mode searches a partly wanted season as a whole', async ({ api }) => {
    test.setTimeout(240_000);
    await importFirstEpisode();

    const instanceId = await arrangeInstance(api, SONARR, { instance: { sonarrSearchMode: 'Season' } });
    const command = await searchOnce(api, instanceId);

    expect(command.name).toBe('SeasonSearch');
    expect(command.body?.seasonNumber).toBe(1);
  });

  test('episode mode searches a fully wanted season one episode at a time', async ({ api }) => {
    test.setTimeout(180_000);
    // S01E01's id equals the series id, which would hide an id mix-up.
    await SONARR.arr.put('/api/v3/episode/monitor', { episodeIds: [findEpisode(episodes, 1).id], monitored: false });

    const instanceId = await arrangeInstance(api, SONARR, { instance: { sonarrSearchMode: 'Episode' } });
    const command = await searchOnce(api, instanceId);

    expect(command.name).toBe('EpisodeSearch');
    expect(command.body?.episodeIds).toEqual([findEpisode(episodes, 2).id]);
  });
});

test.describe('Seeker Whisparr v2 search mode', () => {
  const { yearSearch, singleScene } = TEST_CONFIG.whisparr2Seed;

  let library: Array<Record<string, unknown>>;
  let scenes: Array<{ id: number; monitored: boolean }>;

  async function setMonitored(sceneIds: number[], monitored: boolean): Promise<void> {
    await WHISPARR2.arr.put('/api/v3/episode/monitor', { episodeIds: sceneIds, monitored });
  }

  test.beforeEach(async () => {
    await resetLiveArrState();
    await indexerMock.stub(torznabSearchStub(WHISPARR2.searchMode, []));
    library = await snapshotLibrary(WHISPARR2);
    scenes = await WHISPARR2.arr.get<Array<{ id: number; monitored: boolean }>>(
      `/api/v3/episode?seriesId=${WHISPARR2.itemId}`,
    );
  });

  test.afterEach(async ({ api }) => {
    await teardownInstances(api);
    for (const monitored of [true, false]) {
      const sceneIds = scenes.filter((scene) => scene.monitored === monitored).map((scene) => scene.id);
      if (sceneIds.length > 0) {
        await setMonitored(sceneIds, monitored);
      }
    }
    await restoreLibrary(WHISPARR2, library);
    await resetLiveArrState();
  });

  test('auto searches a fully wanted year as a whole', async ({ api }) => {
    test.setTimeout(180_000);
    await setMonitored([singleScene.monitored.id], false);

    const instanceId = await arrangeInstance(api, WHISPARR2, { instance: { sonarrSearchMode: 'Auto' } });
    const command = await searchOnce(api, instanceId, WHISPARR2);

    expect(command.name).toBe('SeasonSearch');
    expect(command.body?.seasonNumber).toBe(yearSearch.year);
  });

  test('auto searches a partly wanted year one scene at a time', async ({ api }) => {
    test.setTimeout(180_000);
    await setMonitored(yearSearch.scenes.map((scene) => scene.id), false);

    const instanceId = await arrangeInstance(api, WHISPARR2, { instance: { sonarrSearchMode: 'Auto' } });
    const command = await searchOnce(api, instanceId, WHISPARR2);

    expect(command.name).toBe('EpisodeSearch');
    expect(command.body?.episodeIds).toEqual([singleScene.monitored.id]);
  });

  test('episode mode searches a fully wanted year one scene at a time', async ({ api }) => {
    test.setTimeout(180_000);
    await setMonitored([singleScene.monitored.id], false);

    const instanceId = await arrangeInstance(api, WHISPARR2, { instance: { sonarrSearchMode: 'Episode' } });
    const command = await searchOnce(api, instanceId, WHISPARR2);

    expect(command.name).toBe('EpisodeSearch');
    expect(command.body?.episodeIds).toHaveLength(1);
    expect(yearSearch.scenes.map((scene) => scene.id)).toContain(command.body?.episodeIds?.[0]);
  });
});
