import { test, expect, TEST_CONFIG } from '../fixtures/base';
import type { CleanuparrApi } from '../helpers/api';
import { indexerMock } from '../helpers/live-arr';
import {
  LIDARR,
  RADARR,
  RUN_TAG,
  SONARR,
  WHISPARR,
  WHISPARR2,
  TRIGGERED_SEARCH_TIMEOUT,
  arrangeInstance,
  listSearchEvents,
  resetLiveArrState,
  restoreLibrary,
  snapshotLibrary,
  teardownInstances,
  triggerSeeker,
  waitForArrQueue,
} from '../helpers/seeker-live';
import { grabbableRelease, torznabSearchStub } from '../helpers/mocks/torznab-stubs';

/**
 * The eligibility funnel on the search stats summary and the read-only preview.
 *
 * The indexer returns nothing unless a test stubs a release,
 * so a search leaves its item searched without queueing it.
 */

const SKIP_TAG = 'e2e-preview-skip';
const INCLUDE_TAG = 'e2e-preview-include';

const SETTLE_MS = 20_000;

interface Funnel {
  libraryItems: number;
  releasedItems: number;
  monitoredItems: number;
  tagFilteredItems: number;
  eligibleUnits: number;
  queuedUnits: number;
  recentlyGrabbedUnits: number;
  searchedUnits: number;
  remainingUnits: number;
  countedAt: string;
}

interface Preview {
  funnel: Funnel;
  cycleWaitingUntil: string | null;
  randomPick: boolean;
  nextUp: Array<{ name: string; reason: string; contentKind: string | null }>;
  excluded: Array<{ name: string; reason: string }>;
}

async function getPreview(api: CleanuparrApi, instanceId: string): Promise<Preview> {
  const res = await api.seeker.getPreview(instanceId);
  expect(res.status, await res.clone().text()).toBe(200);
  return (await res.json()) as Preview;
}

async function summaryFunnel(api: CleanuparrApi, instanceId: string): Promise<Funnel | null> {
  const body = (await (await api.seeker.getSearchStatsSummary()).json()) as {
    perInstanceStats: Array<{ instanceId: string; funnel: Funnel | null }>;
  };
  return body.perInstanceStats.find((s) => s.instanceId === instanceId)?.funnel ?? null;
}

function exclusionOf(preview: Preview, name: string): string | undefined {
  return preview.excluded.find((e) => e.name.includes(name))?.reason;
}

async function tagSeededMovie(tagLabel: string | null): Promise<void> {
  const tags = tagLabel ? [await RADARR.arr.ensureTag(tagLabel)] : [];
  const movie = await RADARR.arr.get<Record<string, unknown>>(`/api/v3/movie/${RADARR.itemId}`);
  await RADARR.arr.put(`/api/v3/movie/${RADARR.itemId}`, { ...movie, tags });
}

test.describe('Seeker preview and funnel', () => {
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

  test('lists the seeded movie next up without searching it', async ({ api }) => {
    const instanceId = await arrangeInstance(api, RADARR);

    const preview = await getPreview(api, instanceId);

    expect(preview.nextUp.map((u) => u.name)).toContain(RADARR.seededTitle);
    expect(preview.funnel.libraryItems).toBe(library.length);
    expect(preview.funnel.eligibleUnits).toBeGreaterThan(0);
    expect(await listSearchEvents(api, instanceId)).toHaveLength(0);
  });

  test('explains a skipped tag', async ({ api }) => {
    const instanceId = await arrangeInstance(api, RADARR, { instance: { skipTags: [SKIP_TAG] } });
    await tagSeededMovie(SKIP_TAG);

    const preview = await getPreview(api, instanceId);

    expect(exclusionOf(preview, RADARR.seededTitle)).toBe('SkipTag');
    expect(preview.nextUp.map((u) => u.name)).not.toContain(RADARR.seededTitle);
  });

  test('explains a missing include tag', async ({ api }) => {
    const instanceId = await arrangeInstance(api, RADARR, { instance: { includeTags: [INCLUDE_TAG] } });
    await tagSeededMovie(null);

    const preview = await getPreview(api, instanceId);

    expect(exclusionOf(preview, RADARR.seededTitle)).toBe('MissingIncludeTag');
  });

  test('counts the funnel and waits out the cycle after the only search', async ({ api }) => {
    // Seven days keep the searched cycle open, so the preview reports the wait.
    const instanceId = await arrangeInstance(api, RADARR, { instance: { minCycleTimeDays: 7 } });

    await triggerSeeker(api);
    await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(1);

    const funnel = await summaryFunnel(api, instanceId);
    expect(funnel).not.toBeNull();
    expect(funnel!.libraryItems).toBe(library.length);
    expect(funnel!.eligibleUnits).toBe(1);
    expect(funnel!.searchedUnits).toBe(1);
    expect(funnel!.remainingUnits).toBe(0);

    const preview = await getPreview(api, instanceId);
    expect(exclusionOf(preview, RADARR.seededTitle)).toBe('SearchedThisCycle');
    expect(preview.cycleWaitingUntil).not.toBeNull();
    expect(preview.nextUp).toHaveLength(0);
  });

  test('explains a movie waiting in the download queue', async ({ api }) => {
    test.setTimeout(180_000);

    const release = grabbableRelease(RADARR.searchMode, `F1.2025.1080p.WEB-DL.DDP5.1.H.264-E2E${RUN_TAG}`, RADARR.category);
    await indexerMock.resetAll();
    await indexerMock.stubMany(release.mappings);

    const instanceId = await arrangeInstance(api, RADARR, { instance: { minCycleTimeDays: 7 } });

    await triggerSeeker(api);
    await waitForArrQueue(RADARR.arr, release.downloadId, TRIGGERED_SEARCH_TIMEOUT);

    // The queue state moves to downloading once qBittorrent picks the torrent up.
    await expect
      .poll(async () => exclusionOf(await getPreview(api, instanceId), RADARR.seededTitle), { timeout: SETTLE_MS })
      .toBe('InQueue');
  });

  test('previews a Sonarr instance by series', async ({ api }) => {
    const instanceId = await arrangeInstance(api, SONARR);

    const preview = await getPreview(api, instanceId);

    expect(preview.nextUp.length).toBeGreaterThan(0);
    expect(preview.nextUp.every((u) => u.name.startsWith(SONARR.seededTitle))).toBe(true);
    expect(preview.funnel.libraryItems).toBeGreaterThan(0);
  });

  test('returns 404 for an unknown instance', async ({ api }) => {
    const res = await api.seeker.getPreview('00000000-0000-0000-0000-000000000001');

    expect(res.status).toBe(404);
  });
});

test.describe('Seeker preview on Whisparr v3', () => {
  let library: Array<Record<string, unknown>> = [];

  test.beforeEach(async () => {
    await resetLiveArrState();
    library = await snapshotLibrary(WHISPARR);
    await indexerMock.stub(torznabSearchStub(WHISPARR.searchMode, []));
  });

  test.afterEach(async ({ api }) => {
    await teardownInstances(api);
    await restoreLibrary(WHISPARR, library);
    await resetLiveArrState();
  });

  test('lists the seeded movie next up as missing with funnel counts', async ({ api }) => {
    const instanceId = await arrangeInstance(api, WHISPARR);

    const preview = await getPreview(api, instanceId);

    const next = preview.nextUp.find((u) => u.name === WHISPARR.seededTitle);
    expect(next?.reason).toBe('Missing');
    expect(preview.funnel.libraryItems).toBe(library.length);
    expect(preview.funnel.eligibleUnits).toBeGreaterThan(0);
    expect(await listSearchEvents(api, instanceId)).toHaveLength(0);
  });
});

test.describe('Seeker preview on Whisparr v2', () => {
  const { yearSearch, singleScene } = TEST_CONFIG.whisparr2Seed;

  let library: Array<Record<string, unknown>> = [];

  test.beforeEach(async () => {
    await resetLiveArrState();
    library = await snapshotLibrary(WHISPARR2);
    await indexerMock.stub(torznabSearchStub(WHISPARR2.searchMode, []));
  });

  test.afterEach(async ({ api }) => {
    await teardownInstances(api);
    await WHISPARR2.arr.put('/api/v3/episode/monitor', {
      episodeIds: yearSearch.scenes.map((scene) => scene.id),
      monitored: true,
    });
    await restoreLibrary(WHISPARR2, library);
    await resetLiveArrState();
  });

  test('lists the seeded site by year and scene dates with funnel counts', async ({ api }) => {
    const instanceId = await arrangeInstance(api, WHISPARR2);

    const preview = await getPreview(api, instanceId);

    expect(preview.nextUp.map((u) => u.name)).toContain(`${WHISPARR2.seededTitle} ${yearSearch.year}`);
    expect(preview.funnel.libraryItems).toBe(library.length);
    expect(preview.funnel.eligibleUnits).toBeGreaterThan(0);
    expect(await listSearchEvents(api, instanceId)).toHaveLength(0);
  });

  test('lists a single scene by its release date once the year is not fully wanted', async ({ api }) => {
    await WHISPARR2.arr.put('/api/v3/episode/monitor', {
      episodeIds: yearSearch.scenes.map((scene) => scene.id),
      monitored: false,
    });
    const instanceId = await arrangeInstance(api, WHISPARR2);

    const preview = await getPreview(api, instanceId);

    expect(preview.nextUp.map((u) => u.name)).toContain(`${WHISPARR2.seededTitle} ${singleScene.monitored.releaseDate}`);
  });
});

test.describe('Seeker preview on Lidarr', () => {
  let library: Array<Record<string, unknown>> = [];

  test.beforeEach(async () => {
    await resetLiveArrState();
    library = await snapshotLibrary(LIDARR);
    await indexerMock.stub(torznabSearchStub(LIDARR.searchMode, []));
  });

  test.afterEach(async ({ api }) => {
    await teardownInstances(api);
    await restoreLibrary(LIDARR, library);
    await resetLiveArrState();
  });

  test('lists the seeded album next up as missing with artist funnel counts', async ({ api }) => {
    const instanceId = await arrangeInstance(api, LIDARR);

    const preview = await getPreview(api, instanceId);

    const next = preview.nextUp.find((u) => u.name === `${LIDARR.seededTitle} - ${TEST_CONFIG.liveArr.seededAlbumTitle}`);
    expect(next?.reason).toBe('Missing');
    expect(preview.funnel.libraryItems).toBe(library.length);
    expect(preview.funnel.eligibleUnits).toBeGreaterThan(0);
    expect(await listSearchEvents(api, instanceId)).toHaveLength(0);
  });
});
