import { test, expect, TEST_CONFIG } from '../fixtures/base';
import { ArrStubs } from '../helpers/mocks';
import type { MockServers } from '../helpers/mocks';
import type { CleanuparrApi } from '../helpers/api';

/**
 * A season pack is one download with a queue record per episode.
 * When its records span queue pages, one run must still strike it once (#826).
 */

const DOWNLOAD_ID = 'HASH-SEASON-PACK';
const SERIES_ID = 70;
const PACK_EPISODE_IDS = [101, 102, 103];
const FAILED_IMPORT_REASON = 'Unable to determine if file is a sample';
const PAGE_SIZE = 200;
const TOTAL_RECORDS = 401;

const createdInstances: string[] = [];
let savedQueueCleanerConfig: Record<string, unknown> | undefined;

/** A healthy download that no check strikes. */
function fillerRecord(id: number): Record<string, unknown> {
  return {
    id,
    seriesId: SERIES_ID + 1,
    episodeId: 1,
    title: `filler.s01e${id}`,
    status: 'downloading',
    trackedDownloadStatus: 'ok',
    trackedDownloadState: 'downloading',
    downloadId: `HASH-FILLER-${id}`,
    protocol: 'torrent',
    size: 1000,
    sizeleft: 500,
  };
}

/** One record of the season pack, blocked on the same reason force-import.spec.ts uses for a failed import. */
function packRecord(id: number, episodeId: number): Record<string, unknown> {
  return {
    id,
    seriesId: SERIES_ID,
    episodeId,
    title: 'season.pack.s01',
    status: 'completed',
    trackedDownloadStatus: 'warning',
    trackedDownloadState: 'importPending',
    downloadId: DOWNLOAD_ID,
    protocol: 'torrent',
    size: 32768,
    sizeleft: 0,
    statusMessages: [{ title: 'season.pack.s01.mkv', messages: [FAILED_IMPORT_REASON] }],
  };
}

function pageBody(page: number, records: Array<Record<string, unknown>>): string {
  return JSON.stringify({ page, pageSize: PAGE_SIZE, totalRecords: TOTAL_RECORDS, records });
}

async function failedImportStrikes(api: CleanuparrApi, downloadId: string): Promise<number> {
  const res = await api.events.list({ eventType: 'FailedImportStrike', page: 1, pageSize: 500 });
  expect(res.status, 'events query failed').toBe(200);

  const body: { items?: Array<{ itemHash?: string }> } = await res.json();

  return (body.items ?? []).filter((e) => (e.itemHash ?? '').toLowerCase() === downloadId.toLowerCase()).length;
}

test.describe('QueueCleaner season pack across queue pages', () => {
  test.afterEach(async ({ api }) => {
    for (const id of createdInstances.splice(0)) {
      await api.arr.deleteInstance('sonarr', id);
    }

    if (savedQueueCleanerConfig) {
      const restored = await api.queueCleaner.updateConfig(savedQueueCleanerConfig);
      expect(restored.ok).toBe(true);
      savedQueueCleanerConfig = undefined;
    }
  });

  test('strikes a pack split across pages once per run, not once per page', async ({ api, mocks }) => {
    test.setTimeout(120_000);

    await ArrStubs.applyArrDefaults(mocks.arr);

    const page1Fillers = Array.from({ length: 199 }, (_, i) => fillerRecord(i + 1));
    const page2Fillers = Array.from({ length: 199 }, (_, i) => fillerRecord(i + 200));

    await mocks.arr.stub(
      ArrStubs.arrRawQueuePageStub(1, pageBody(1, [...page1Fillers, packRecord(1000, PACK_EPISODE_IDS[0])])),
    );
    await mocks.arr.stub(
      ArrStubs.arrRawQueuePageStub(2, pageBody(2, [...page2Fillers, packRecord(1001, PACK_EPISODE_IDS[1])])),
    );
    await mocks.arr.stub(ArrStubs.arrRawQueuePageStub(3, pageBody(3, [packRecord(1002, PACK_EPISODE_IDS[2])])));

    const current = await (await api.queueCleaner.getConfig()).json();
    savedQueueCleanerConfig = current;
    const updated = await api.queueCleaner.updateConfig({
      ...current,
      failedImport: {
        ...current.failedImport,
        maxStrikes: 3,
        // Exclude with no pattern strikes everything, which Include would refuse to do.
        patternMode: 'Exclude',
        patterns: [],
        forceImport: false,
      },
    });
    expect(updated.ok, `queue cleaner updateConfig: ${updated.status} ${await updated.text()}`).toBe(true);

    const created = await api.arr.createInstance('sonarr', {
      name: 'sonarr-season-pack-paging',
      url: TEST_CONFIG.mocks.arrUrl,
      apiKey: 'k',
      version: 4,
      enabled: true,
    });
    expect(created.ok, `createInstance: ${created.status}`).toBe(true);
    createdInstances.push((await created.json()).id);

    const trigger = await api.jobs.trigger('QueueCleaner');
    expect(trigger.status).toBeLessThan(300);

    // Page 3 is requested only after the earlier pages, so any per-page strike has happened by then.
    await expect
      .poll(
        async () => {
          const requests = await mocks.arr.findRequests({ method: 'GET', urlPath: '/api/v3/queue' });
          const pageThreeRead = requests.some((r) => new URL(r.url, 'http://x').searchParams.get('page') === '3');

          return pageThreeRead && (await failedImportStrikes(api, DOWNLOAD_ID)) > 0;
        },
        { timeout: 60_000, intervals: [1_000] },
      )
      .toBe(true);

    expect(await failedImportStrikes(api, DOWNLOAD_ID)).toBe(1);
    expect(await mocks.arr.findRequests({ method: 'DELETE', urlPattern: '/api/v3/queue/.*' })).toHaveLength(0);
  });
});
