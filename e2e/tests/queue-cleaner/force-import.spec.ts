import { test, expect, TEST_CONFIG } from '../fixtures/base';
import { ArrStubs } from '../helpers/mocks';
import type { MockServers } from '../helpers/mocks';
import type { CleanuparrApi } from '../helpers/api';

/**
 * Force import through the Queue Cleaner, against a stubbed Sonarr.
 *
 * Each case pins one decision: import the download, or leave it struck.
 */

/** One of the reasons Cleanuparr treats as safe to force past. */
const SAMPLE_REASON = 'Unable to determine if file is a sample';
const RECORD_ID = 5150;
const SERIES_ID = 7;
const EPISODE_ID = 9;

const createdInstances: string[] = [];

function queueBody(downloadId: string): string {
  return JSON.stringify({
    page: 1,
    pageSize: 50,
    totalRecords: 1,
    records: [
      {
        id: RECORD_ID,
        seriesId: SERIES_ID,
        episodeId: EPISODE_ID,
        title: 'force.import.s01e01',
        status: 'completed',
        trackedDownloadStatus: 'warning',
        // The state a real Sonarr reports for a sample-detection block.
        trackedDownloadState: 'importPending',
        downloadId,
        protocol: 'torrent',
        size: 32768,
        sizeleft: 0,
        statusMessages: [{ title: 'force.import.s01e01.mkv', messages: [SAMPLE_REASON] }],
      },
    ],
  });
}

function candidate(overrides: Record<string, unknown> = {}): Record<string, unknown> {
  return {
    id: 1,
    path: '/downloads/force.import.s01e01.mkv',
    relativePath: 'force.import.s01e01.mkv',
    folderName: null,
    downloadId: 'HASH-FORCE-IMPORT',
    indexerFlags: 0,
    releaseType: 'singleEpisode',
    quality: { quality: { id: 3, name: 'WEBDL-1080p' }, revision: { version: 1, real: 0 } },
    languages: [{ id: 1, name: 'English' }],
    series: { id: SERIES_ID },
    episodes: [{ id: EPISODE_ID }],
    rejections: [{ reason: SAMPLE_REASON, type: 'permanent' }],
    ...overrides,
  };
}

async function arrange(
  api: CleanuparrApi,
  mocks: MockServers,
  name: string,
  downloadId: string,
  candidates: Array<Record<string, unknown>>,
): Promise<void> {
  await ArrStubs.applyArrDefaults(mocks.arr);
  await mocks.arr.stub(ArrStubs.arrRawQueueStub(queueBody(downloadId)));
  await mocks.arr.stub(ArrStubs.arrManualImportStub(candidates));
  await mocks.arr.stub(ArrStubs.arrCommandTriggerStub());
  // Force import will not act while the arr reports import work in flight.
  await mocks.arr.stub(ArrStubs.arrCommandListStub([]));

  const current = await (await api.queueCleaner.getConfig()).json();
  const updated = await api.queueCleaner.updateConfig({
    ...current,
    failedImport: {
      ...current.failedImport,
      // Striking stays off, so only force import can act on the record.
      maxStrikes: 0,
      forceImport: true,
    },
  });
  expect(updated.ok, `queue cleaner updateConfig: ${updated.status} ${await updated.text()}`).toBe(true);

  const created = await api.arr.createInstance('sonarr', {
    name,
    url: TEST_CONFIG.mocks.arrUrl,
    apiKey: 'k',
    version: 4,
    enabled: true,
  });
  expect(created.ok, `createInstance: ${created.status}`).toBe(true);
  createdInstances.push((await created.json()).id);
}

async function manualImportCommands(mocks: MockServers): Promise<Array<Record<string, any>>> {
  const requests = await mocks.arr.findRequests({ method: 'POST', urlPath: '/api/v3/command' });

  return requests
    .map((request) => (request.body ? JSON.parse(request.body) : {}))
    .filter((body) => body.Name === 'ManualImport');
}

/** Runs the job until it acts, since importPending needs a second sighting first. */
async function runUntilImport(api: CleanuparrApi, mocks: MockServers): Promise<Array<Record<string, any>>> {
  await expect
    .poll(
      async () => {
        await api.jobs.trigger('QueueCleaner');
        return (await manualImportCommands(mocks)).length;
      },
      { timeout: 120_000, intervals: [2_000] },
    )
    .toBeGreaterThan(0);

  return manualImportCommands(mocks);
}

/** Three runs are more than the two a rescue needs, so a refusal is a real one. */
async function runThreeTimes(api: CleanuparrApi): Promise<void> {
  for (let run = 0; run < 3; run++) {
    await api.jobs.trigger('QueueCleaner');
    await new Promise((r) => setTimeout(r, 3_000));
  }
}

test.describe.serial('QueueCleaner force import', () => {
  test.afterEach(async ({ api }) => {
    for (const id of createdInstances.splice(0)) {
      await api.arr.deleteInstance('sonarr', id);
    }
  });

  test('imports a blocked download whose every reason is safe to force past', async ({ api, mocks }) => {
    test.setTimeout(180_000);

    await arrange(api, mocks, 'sonarr-force-import', 'HASH-FORCE-IMPORT', [candidate()]);

    const commands = await runUntilImport(api, mocks);

    const file = commands[0].Files[0];
    expect(file.Path).toBe('/downloads/force.import.s01e01.mkv');
    expect(file.SeriesId).toBe(SERIES_ID);
    expect(file.EpisodeIds).toEqual([EPISODE_ID]);
    expect(file.MovieId).toBeUndefined();
    expect(commands[0].ImportMode).toBe('auto');

    // A rescued download is never removed.
    expect(await mocks.arr.findRequests({ method: 'DELETE', urlPattern: '/api/v3/queue/.*' })).toHaveLength(0);
  });

  test('refuses a file that maps to another series', async ({ api, mocks }) => {
    test.setTimeout(180_000);

    await arrange(api, mocks, 'sonarr-force-import-mismatch', 'HASH-FORCE-IMPORT-MISMATCH', [
      candidate({ series: { id: SERIES_ID + 1 } }),
    ]);

    await runThreeTimes(api);

    expect(await manualImportCommands(mocks)).toHaveLength(0);
  });

  test('refuses a candidate carrying an unrecognised reason', async ({ api, mocks }) => {
    test.setTimeout(180_000);

    await arrange(api, mocks, 'sonarr-force-import-rejected', 'HASH-FORCE-IMPORT-REJECTED', [
      candidate({
        rejections: [
          { reason: SAMPLE_REASON, type: 'permanent' },
          { reason: 'Not an upgrade for existing episode file(s)', type: 'permanent' },
        ],
      }),
    ]);

    await runThreeTimes(api);

    expect(await manualImportCommands(mocks)).toHaveLength(0);
  });
});
