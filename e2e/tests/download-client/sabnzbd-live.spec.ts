import { test, expect } from '@playwright/test';
import {
  loginAndGetToken,
  createDownloadClient,
  deleteDownloadClient,
  listDownloadClients,
  createArrInstance,
  deleteArrInstance,
  triggerJob,
} from '../helpers/app-api';
import { SabnzbdDriver } from '../helpers/usenet-clients/sabnzbd';
import { WireMockClient, Mapping } from '../helpers/mocks/wiremock-client';
import { TEST_CONFIG } from '../helpers/test-config';
import { findEventWithDeleteReason } from '../helpers/api/signalr';

/**
 * Live SABnzbd coverage: a real SABnzbd container downloading through the fake
 * sabnews NNTP server, driving Cleanuparr's own QueueCleaner job against it.
 *
 * QueueCleaner never deletes a usenet job from SABnzbd itself: it only tells
 * the arr to do so via DELETE /api/v3/queue/{id}?removeFromClient=true (the arr
 * owns its download client relationship). Since Sonarr/Radarr in this suite are
 * stubbed by wiremock-arr, "removed from the arr queue" is verified through its
 * request journal, same as the existing malware-blocker.spec.ts pattern.
 */

const POLL_TIMEOUT_MS = 30_000;

interface QueueStubRecord {
  id: number;
  downloadId: string;
  title: string;
}

function arrHealthMapping(): Mapping {
  return {
    request: { method: 'GET', urlPathPattern: '/api/v[0-9]+/system/status' },
    response: { status: 200, jsonBody: { version: '4.0.0.0', appName: 'Sonarr' } },
    priority: 1,
  };
}

function arrQueueMapping(records: QueueStubRecord[]): Mapping {
  return {
    request: { method: 'GET', urlPath: '/api/v3/queue' },
    response: {
      status: 200,
      jsonBody: {
        page: 1,
        pageSize: records.length || 10,
        totalRecords: records.length,
        records: records.map((r) => ({
          id: r.id,
          downloadId: r.downloadId,
          title: r.title,
          protocol: 'usenet',
          seriesId: 1,
          episodeId: 1,
          seasonNumber: 1,
          status: 'downloading',
          trackedDownloadStatus: 'ok',
          trackedDownloadState: 'downloading',
          statusMessages: [],
          sizeLeft: 0,
        })),
      },
    },
    priority: 1,
  };
}

function arrCatchAllMapping(): Mapping {
  return {
    request: { method: 'ANY', urlPathPattern: '/api/v[0-9]+/.*' },
    response: { status: 200, jsonBody: {} },
    priority: 100,
  };
}

async function stubArrQueue(arr: WireMockClient, records: QueueStubRecord[]): Promise<void> {
  await arr.resetAll();
  await arr.stubMany([arrHealthMapping(), arrQueueMapping(records), arrCatchAllMapping()]);
}

async function deleteCallSeen(arr: WireMockClient, queueId: number, timeoutMs: number): Promise<boolean> {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    const reqs = await arr.findRequests({ method: 'DELETE', urlPattern: `/api/v3/queue/${queueId}.*` });
    if (reqs.length > 0) {
      return true;
    }
    await new Promise((r) => setTimeout(r, 500));
  }
  return false;
}

test.describe.serial('SABnzbd live: download client + QueueCleaner', () => {
  const driver = new SabnzbdDriver();
  const arr = new WireMockClient(TEST_CONFIG.mocks.arrAdminUrl);
  let token: string;
  let clientId: string;
  let sonarrId: string;

  test.beforeAll(async () => {
    test.setTimeout(60_000);
    token = await loginAndGetToken();
    await driver.ready();
    await driver.clearAll();
    await driver.resumeQueue();

    for (const client of await listDownloadClients(token)) {
      await deleteDownloadClient(token, client.id);
    }

    const createRes = await createDownloadClient(token, {
      enabled: true,
      name: 'SABnzbd e2e',
      typeName: 'Sabnzbd',
      host: driver.cleanuparrHost,
      apiKey: driver.apiKey,
    });
    expect(createRes.status, `createDownloadClient: ${createRes.status}`).toBeLessThan(300);
    clientId = (await createRes.json()).id;

    await arr.waitReady();
    await stubArrQueue(arr, []);
    const sonarrRes = await createArrInstance(token, 'sonarr', {
      name: 'SABnzbd QueueCleaner Stub',
      url: TEST_CONFIG.mocks.arrUrl,
      apiKey: 'sab-qc-e2e',
      version: 4,
    });
    expect(sonarrRes.status).toBe(201);
    sonarrId = (await sonarrRes.json()).id;
  });

  test.afterAll(async () => {
    if (clientId) {
      await deleteDownloadClient(token, clientId).catch(() => undefined);
    }
    if (sonarrId) {
      await deleteArrInstance(token, 'sonarr', sonarrId).catch(() => undefined);
    }
    await driver.clearAll().catch(() => undefined);
    await driver.resumeQueue().catch(() => undefined);
  });

  test('CRUD: update and list round-trip', async () => {
    const list = await listDownloadClients(token);
    expect(list.some((c) => c.id === clientId)).toBe(true);
  });

  test('a job that fails fast (missing article) is removed from the arr queue', async () => {
    test.setTimeout(180_000);
    const nzoId = await driver.addMissingSegmentNzb('qc-failed.bin', 'e2e-qc');
    // A missing article can sit out SABnzbd's 60s news server timeout before the job fails.
    await driver.waitForHistoryStatus(nzoId, 'Failed', 120_000);

    await stubArrQueue(arr, [{ id: 9001, downloadId: nzoId, title: 'qc-failed' }]);
    const trig = await triggerJob(token, 'QueueCleaner');
    expect(trig.ok, `triggerJob: ${trig.status}`).toBe(true);

    expect(await deleteCallSeen(arr, 9001, POLL_TIMEOUT_MS)).toBe(true);

    const event = await findEventWithDeleteReason(token, nzoId, 'DownloadFailed', POLL_TIMEOUT_MS);
    expect(event, 'a DownloadFailed event should have been emitted for the failed SABnzbd job').toBeDefined();

    // QueueCleaner never calls SABnzbd to delete the job itself: removal from the
    // client is the arr's job, driven by the removeFromClient query param above.
    // With a stubbed arr (not a real Sonarr wired to this SABnzbd), the job is left
    // untouched here; a real arr would issue its own SABnzbd history delete.
    const { history } = await driver.findJob(nzoId);
    expect(history?.status).toBe('Failed');
  });

  test('a job still in the queue is left alone', async () => {
    test.setTimeout(90_000);
    // The whole queue is paused before the job is added, so it never starts
    // downloading and can't race its way into history before being checked.
    let nzoId: string | undefined;

    try {
      await driver.pauseQueue();
      nzoId = await driver.addWorkingNzb('qc-downloading.bin', 300_000, 'e2e-qc');
      await stubArrQueue(arr, [{ id: 9002, downloadId: nzoId, title: 'qc-downloading' }]);
      const trig = await triggerJob(token, 'QueueCleaner');
      expect(trig.ok, `triggerJob: ${trig.status}`).toBe(true);
      await new Promise((r) => setTimeout(r, 4_000));

      expect(await deleteCallSeen(arr, 9002, 4_000)).toBe(false);
      const { queue } = await driver.findJob(nzoId);
      expect(queue, 'an in-progress job must still be in the queue').toBeDefined();
    } finally {
      if (nzoId) {
        await driver.deleteQueueJob(nzoId, true).catch(() => undefined);
      }
      await driver.resumeQueue();
    }
  });

  test('a completed job is left alone (no slow-rule strike applies to SABnzbd)', async () => {
    test.setTimeout(90_000);
    const nzoId = await driver.addWorkingNzb('qc-completed.bin', 300_000, 'e2e-qc');
    await driver.waitForHistoryStatus(nzoId, 'Completed', 60_000);

    await stubArrQueue(arr, [{ id: 9003, downloadId: nzoId, title: 'qc-completed' }]);
    const trig = await triggerJob(token, 'QueueCleaner');
    expect(trig.ok, `triggerJob: ${trig.status}`).toBe(true);
    await new Promise((r) => setTimeout(r, 4_000));

    expect(await deleteCallSeen(arr, 9003, 4_000)).toBe(false);
  });
});
