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

function emptyQueueBody(): string {
  return JSON.stringify({ page: 1, pageSize: 50, totalRecords: 0, records: [] });
}

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

interface ArrangeOptions {
  /** Striking stays off by default, so only force import can act on the record. */
  maxStrikes?: number;
  forceImportMaxTries?: number;
}

async function arrange(
  api: CleanuparrApi,
  mocks: MockServers,
  name: string,
  downloadId: string,
  candidates: Array<Record<string, unknown>>,
  options: ArrangeOptions = {},
): Promise<void> {
  await ArrStubs.applyArrDefaults(mocks.arr);
  await mocks.arr.stub(ArrStubs.arrRawQueueStub(queueBody(downloadId)));
  await mocks.arr.stub(ArrStubs.arrManualImportStub(downloadId, candidates));
  await mocks.arr.stub(ArrStubs.arrCommandTriggerStub());
  // Force import will not act while the arr reports import work in flight.
  await mocks.arr.stub(ArrStubs.arrCommandListStub([]));

  const current = await (await api.queueCleaner.getConfig()).json();
  const updated = await api.queueCleaner.updateConfig({
    ...current,
    failedImport: {
      ...current.failedImport,
      maxStrikes: options.maxStrikes ?? 0,
      // Exclude with no pattern strikes everything, which Include would refuse to do.
      patternMode: 'Exclude',
      patterns: [],
      forceImport: true,
      forceImportMaxTries: options.forceImportMaxTries ?? 3,
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

async function manualImportCommands(
  mocks: MockServers,
  downloadId?: string,
): Promise<Array<Record<string, any>>> {
  const requests = await mocks.arr.findRequests({ method: 'POST', urlPath: '/api/v3/command' });

  return requests
    .map((request) => (request.body ? JSON.parse(request.body) : {}))
    .filter((body) => body.Name === 'ManualImport')
    .filter(
      (body) =>
        downloadId === undefined ||
        (body.Files ?? []).some((file: Record<string, any>) => file.DownloadId === downloadId),
    );
}

/** A leftover run from an earlier case can remove its own download, so strikes are read per download. */
async function failedImportStrikes(api: CleanuparrApi, downloadId: string): Promise<number> {
  const res = await api.events.list({ eventType: 'FailedImportStrike', page: 1, pageSize: 500 });
  expect(res.status, 'events query failed').toBe(200);

  const body: { items?: Array<{ itemHash?: string }> } = await res.json();

  return (body.items ?? []).filter((e) => (e.itemHash ?? '').toLowerCase() === downloadId.toLowerCase()).length;
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

async function forceImportedEvents(api: CleanuparrApi, downloadId: string): Promise<number> {
  const res = await api.events.list({ eventType: 'ForceImported', page: 1, pageSize: 500 });
  expect(res.status, 'events query failed').toBe(200);

  const body: { items?: Array<{ itemHash?: string }> } = await res.json();

  return (body.items ?? []).filter((e) => (e.itemHash ?? '').toLowerCase() === downloadId.toLowerCase()).length;
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

  test('stops asking after the try limit and lets the strikes run', async ({ api, mocks }) => {
    test.setTimeout(300_000);

    const downloadId = 'HASH-FORCE-IMPORT-LIMIT';
    await arrange(api, mocks, 'sonarr-force-import-limit', downloadId, [candidate({ downloadId })], {
      maxStrikes: 3,
      forceImportMaxTries: 2,
    });

    // The arr never drops the download, so every try fails.
    await expect
      .poll(
        async () => {
          await api.jobs.trigger('QueueCleaner');
          return failedImportStrikes(api, downloadId);
        },
        { timeout: 240_000, intervals: [2_000] },
      )
      .toBeGreaterThan(0);

    expect(await manualImportCommands(mocks, downloadId)).toHaveLength(2);
  });

  test('spends a try on a refused import and lets the strikes run', async ({ api, mocks }) => {
    test.setTimeout(300_000);

    const downloadId = 'HASH-FORCE-IMPORT-REFUSED';
    await arrange(api, mocks, 'sonarr-force-import-refused', downloadId, [candidate({ downloadId })], {
      maxStrikes: 3,
      forceImportMaxTries: 2,
    });

    // An arr that refuses every request must not hold the download out of the strike path.
    await mocks.arr.stub(ArrStubs.arrManualImportRefusedStub());

    await expect
      .poll(
        async () => {
          await api.jobs.trigger('QueueCleaner');
          return failedImportStrikes(api, downloadId);
        },
        { timeout: 240_000, intervals: [2_000] },
      )
      .toBeGreaterThan(0);

    // The refused tries were spent, so the arr was asked exactly twice.
    expect(await manualImportCommands(mocks, downloadId)).toHaveLength(2);
  });

  test('reports the import once the arr drops the download', async ({ api, mocks }) => {
    test.setTimeout(300_000);

    const downloadId = 'HASH-FORCE-IMPORT-REPORTED';
    await arrange(api, mocks, 'sonarr-force-import-reported', downloadId, [candidate({ downloadId })]);

    await runUntilImport(api, mocks);

    // Nothing is announced until the arr proves the import by dropping it.
    expect(await forceImportedEvents(api, downloadId)).toBe(0);

    await mocks.arr.stub(ArrStubs.arrRawQueueStub(emptyQueueBody()));

    await expect
      .poll(
        async () => {
          await api.jobs.trigger('QueueCleaner');
          return forceImportedEvents(api, downloadId);
        },
        { timeout: 120_000, intervals: [2_000] },
      )
      .toBe(1);

    // Later runs must not announce it again.
    await api.jobs.trigger('QueueCleaner');
    await new Promise((r) => setTimeout(r, 3_000));

    expect(await forceImportedEvents(api, downloadId)).toBe(1);
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
