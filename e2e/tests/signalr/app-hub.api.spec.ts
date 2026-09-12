import type { HubConnection } from '@microsoft/signalr';
import { test, expect, TEST_CONFIG } from '../fixtures/base';
import { buildHubConnection, waitForEvent } from '../helpers/api/signalr';
import { expectKeys } from '../helpers/contract';
import { ArrStubs } from '../helpers/mocks';
import { adminTokens } from '../helpers/test-lifecycle';

const DOWNLOAD_ID = 'HASH-SIGNALR-WIRE';
const TITLE = 'signalr.wire.shape.s01e01';
const RECORD_ID = 9101;

// No seriesId and no episodeId, so the removal skips the search and files a manual event.
const QUEUE_BODY = `{
  "page": 1,
  "pageSize": 50,
  "totalRecords": 1,
  "records": [
    {
      "id": ${RECORD_ID},
      "title": "${TITLE}",
      "status": "completed",
      "trackedDownloadStatus": "warning",
      "trackedDownloadState": "importFailed",
      "downloadId": "${DOWNLOAD_ID}",
      "protocol": "torrent",
      "size": 1000,
      "sizeleft": 0,
      "statusMessages": [
        { "title": "${TITLE}", "messages": ["Unable to import automatically"] }
      ]
    }
  ]
}`;

const EVENT_KEYS = [
  'arrInstanceId',
  'cleanReason',
  'cleanedCategory',
  'completedAt',
  'cycleId',
  'deleteReason',
  'downloadClientId',
  'eventType',
  'failedImportReasons',
  'grabbedItems',
  'id',
  'isCategoryTag',
  'isDryRun',
  'itemHash',
  'itemTitle',
  'jobRunId',
  'message',
  'newCategory',
  'oldCategory',
  'removeFromClient',
  'searchReason',
  'searchStatus',
  'searchType',
  'seedRatio',
  'seedingTimeHours',
  'severity',
  'strikeCount',
  'strikeId',
  'timestamp',
  'trackingId',
];

const MANUAL_EVENT_KEYS = [
  'downloadClientName',
  'downloadClientType',
  'id',
  'instanceType',
  'instanceUrl',
  'isDryRun',
  'isResolved',
  'itemHash',
  'itemTitle',
  'jobRunId',
  'message',
  'resolvedAt',
  'severity',
  'strikeCount',
  'timestamp',
  'type',
];

const STRIKE_KEYS = ['createdAt', 'downloadId', 'id', 'isDryRun', 'title', 'type'];

const LOG_KEYS = [
  'category',
  'downloadClientName',
  'downloadClientType',
  'exception',
  'instanceName',
  'jobName',
  'jobRunId',
  'level',
  'message',
  'timestamp',
];

const JOB_KEYS = ['jobType', 'name', 'nextRunTime', 'previousRunTime', 'schedule', 'status'];

function connect(): HubConnection {
  return buildHubConnection({
    accessToken: adminTokens().accessToken,
    hubUrl: '/api/hubs/app',
  });
}

async function hubArray(
  connection: HubConnection,
  method: string,
  eventName: string,
  ...args: unknown[]
): Promise<unknown[]> {
  const received = waitForEvent<unknown[]>(connection, eventName);
  await connection.invoke(method, ...args);
  return received;
}

/** The hub replies with an empty array until the job run lands its rows. */
async function hubArrayWhenFilled(
  connection: HubConnection,
  method: string,
  eventName: string,
  ...args: unknown[]
): Promise<unknown[]> {
  let payload: unknown[] = [];
  await expect
    .poll(
      async () => {
        payload = await hubArray(connection, method, eventName, ...args);
        return payload.length;
      },
      { timeout: 90_000, intervals: [1_000] },
    )
    .toBeGreaterThan(0);
  return payload;
}

test.describe('SignalR — app hub', () => {
  test('hub replies keep their wire shape', async ({ api, mocks }) => {
    test.setTimeout(180_000);

    await ArrStubs.applyArrDefaults(mocks.arr);
    await mocks.arr.stub(ArrStubs.arrRawQueueStub(QUEUE_BODY));
    await mocks.arr.stub({
      request: { method: 'DELETE', urlPathPattern: '/api/v3/queue/.*' },
      response: { status: 200, jsonBody: {} },
    });

    const current = await (await api.queueCleaner.getConfig()).json();
    const qc = await api.queueCleaner.updateConfig({
      ...current,
      processNoContentId: true,
      failedImport: {
        ...current.failedImport,
        maxStrikes: 3,
        patternMode: 'Include',
        patterns: ['Unable to import automatically'],
      },
    });
    expect(qc.ok, `queue cleaner updateConfig: ${qc.status}`).toBe(true);

    // One strike is enough to remove, so a single job run fills events and strikes.
    const cfg = await api.arr.updateConfig('sonarr', { failedImportMaxStrikes: 1 });
    expect(cfg.ok, `arr updateConfig: ${cfg.status}`).toBe(true);

    const created = await api.arr.createInstance('sonarr', {
      name: 'sonarr-signalr-wire',
      url: TEST_CONFIG.mocks.arrUrl,
      apiKey: 'k',
      version: 3,
      enabled: true,
    });
    expect(created.ok, `createInstance: ${created.status}`).toBe(true);

    const trigger = await api.jobs.trigger('QueueCleaner');
    expect(trigger.status).toBeLessThan(300);

    const connection = connect();
    await connection.start();
    expect(connection.state).toBe('Connected');

    try {
      const events = await hubArrayWhenFilled(connection, 'GetRecentEvents', 'EventsReceived', 5);
      expectKeys(events[0], EVENT_KEYS);

      const manualEvents = await hubArrayWhenFilled(
        connection,
        'GetRecentManualEvents',
        'ManualEventsReceived',
        100,
      );
      expectKeys(manualEvents[0], MANUAL_EVENT_KEYS);

      const strikes = await hubArrayWhenFilled(connection, 'GetRecentStrikes', 'StrikesReceived', 5);
      expectKeys(strikes[0], STRIKE_KEYS);
      expect(strikes[0]).toMatchObject({ title: TITLE });

      const logs = await hubArrayWhenFilled(connection, 'GetRecentLogs', 'LogsReceived');
      expectKeys(logs[0], LOG_KEYS);

      const jobs = await hubArrayWhenFilled(connection, 'GetJobStatus', 'JobsStatusUpdate');
      expectKeys(jobs[0], JOB_KEYS);
    } finally {
      await connection.stop();
    }
  });

  test('AppStatusUpdated is pushed on connect', async () => {
    const connection = connect();
    const statusPromise = waitForEvent<Record<string, unknown>>(connection, 'AppStatusUpdated');
    await connection.start();

    try {
      expectKeys(await statusPromise, ['currentVersion', 'latestVersion']);
    } finally {
      await connection.stop();
    }
  });

  test('connection without token is rejected', async () => {
    const connection = buildHubConnection({
      accessToken: '',
      hubUrl: '/api/hubs/app',
    });
    await expect(connection.start()).rejects.toThrow();
  });
});
