import { test, expect, TEST_CONFIG } from '../fixtures/base';
import { ArrStubs, DownloadClientStubs } from '../helpers/mocks';
import type { QueueRecord } from '../helpers/mocks/arr-stubs';
import { buildDownloadClientPayload } from '../helpers/api/download-client';
import { findEventWithDeleteReason } from '../helpers/api/signalr';
import { adminTokens } from '../helpers/test-lifecycle';

/** A Sonarr queue record needs a content id (seriesId/episodeId) or QueueCleaner skips it outright. */
function sabQueueRecord(record: QueueRecord): QueueRecord & { seriesId: number; episodeId: number } {
  return { seriesId: 1, episodeId: 1, ...record };
}

// Setup resets app state only between spec folders, and every leftover instance reads the same arr mock.
test.afterEach(async ({ api }) => {
  const config: { instances?: Array<{ id: string }> } = await (await api.arr.getConfig('sonarr')).json();
  for (const instance of config.instances ?? []) {
    await api.arr.deleteInstance('sonarr', instance.id);
  }
});

test.describe('QueueCleaner — job execution end-to-end', () => {
  test('manual trigger is accepted', async ({ api, mocks }) => {
    await ArrStubs.applyArrDefaults(mocks.arr);

    await api.arr.createInstance('sonarr', {
      name: 'sonarr-job',
      url: TEST_CONFIG.mocks.arrUrl,
      apiKey: 'k',
      version: 3,
      enabled: true,
    });

    const trigger = await api.jobs.trigger('QueueCleaner');
    expect(trigger.status).toBeLessThan(300);
  });

  test('manual trigger with a stall rule configured is accepted', async ({ api, mocks }) => {
    await ArrStubs.applyArrDefaults(mocks.arr);
    await mocks.arr.stub(
      ArrStubs.arrQueueStub([
        {
          id: 1,
          title: 'stalled.test.s01e01',
          status: 'warning',
          trackedDownloadStatus: 'warning',
          trackedDownloadState: 'stalled',
          errorMessage: 'No connections',
          downloadId: 'HASH-STALLED',
          protocol: 'torrent',
        },
      ]),
    );

    const sonarr = await (
      await api.arr.createInstance('sonarr', {
        name: 'sonarr-job-strike',
        url: TEST_CONFIG.mocks.arrUrl,
        apiKey: 'k',
        version: 3,
        enabled: true,
      })
    ).json();
    expect(sonarr.id).toBeTruthy();

    const created = await (
      await api.queueCleaner.createRule('stall', {
        name: 'stall-rule-job',
        enabled: true,
        maxStrikes: 3,
        privacyType: 'Public',
        minCompletionPercentage: 0,
        maxCompletionPercentage: 100,
        deletePrivateTorrentsFromClient: false,
        changeCategory: false,
        resetStrikesOnProgress: true,
        minimumProgress: null,
      })
    ).json();

    const trigger = await api.jobs.trigger('QueueCleaner');
    expect(trigger.status).toBeLessThan(300);

    // Explicit cleanup of the created rule — autoReset wipes stall_rules, but
    // EF Core's pooled connection sometimes retains a pre-DELETE snapshot for
    // a moment, which makes the very next test see a phantom overlap. Calling
    // the DELETE endpoint forces the backend itself to clear the row.
    if (created?.id) {
      await api.queueCleaner.deleteRule('stall', created.id);
    }
  });
});

// SABnzbd has no live container in this mocked suite, so every case below drives the
// client through wiremock-dlc. QueueCleaner never deletes from the client itself for a
// usenet job: it asks the arr to do it via DELETE /api/v3/queue/{id}?removeFromClient=.
test.describe('QueueCleaner: SABnzbd job execution (mocked)', () => {
  async function registerSabClient(api: import('../fixtures/base').CleanuparrApi): Promise<string> {
    const created = await (
      await api.downloadClient.create(
        buildDownloadClientPayload('sabnzbd', {
          name: `sab-qc-${Date.now()}`,
          host: TEST_CONFIG.mocks.downloadClientUrl,
          apiKey: 'e2e-key',
        }),
      )
    ).json();
    return created.id;
  }

  test('a failed history job is removed from the arr queue', async ({ api, mocks }) => {
    await ArrStubs.applyArrDefaults(mocks.arr);
    await mocks.downloadClient.stub(DownloadClientStubs.sabQueueStub([]));
    await mocks.downloadClient.stub(
      DownloadClientStubs.sabHistoryStub([{ nzo_id: 'SAB-FAILED-1', status: 'Failed', fail_message: 'Download failed' }]),
    );
    await mocks.arr.stub(
      ArrStubs.arrQueueStub([
        sabQueueRecord({ id: 501, title: 'sab.failed.job', status: 'warning', downloadId: 'SAB-FAILED-1', protocol: 'usenet' }),
      ]),
    );
    await mocks.arr.stub(ArrStubs.arrQueueDeleteStub());

    const clientId = await registerSabClient(api);
    await api.arr.createInstance('sonarr', {
      name: 'sonarr-sab-qc-failed', url: TEST_CONFIG.mocks.arrUrl, apiKey: 'k', version: 3, enabled: true,
    });

    const trigger = await api.jobs.trigger('QueueCleaner');
    expect(trigger.status).toBeLessThan(300);

    await expect(async () => {
      const deletes = await mocks.arr.findRequests({ method: 'DELETE', urlPattern: '/api/v3/queue/501.*' });
      expect(deletes.length).toBeGreaterThan(0);
    }).toPass({ timeout: 15_000 });

    const event = await findEventWithDeleteReason(adminTokens().accessToken, 'SAB-FAILED-1', 'DownloadFailed', 15_000);
    expect(event, 'a DownloadFailed event should have been emitted for the failed SABnzbd job').toBeDefined();

    await api.downloadClient.delete(clientId);
  });

  test('an in-progress queue job is left in the arr queue', async ({ api, mocks }) => {
    await ArrStubs.applyArrDefaults(mocks.arr);
    await mocks.downloadClient.stub(DownloadClientStubs.sabQueueStub([{ nzo_id: 'SAB-DOWNLOADING-1', status: 'Downloading' }]));
    await mocks.downloadClient.stub(DownloadClientStubs.sabHistoryStub([]));
    await mocks.arr.stub(
      ArrStubs.arrQueueStub([
        sabQueueRecord({ id: 502, title: 'sab.downloading.job', status: 'downloading', downloadId: 'SAB-DOWNLOADING-1', protocol: 'usenet' }),
      ]),
    );

    const clientId = await registerSabClient(api);
    await api.arr.createInstance('sonarr', {
      name: 'sonarr-sab-qc-downloading', url: TEST_CONFIG.mocks.arrUrl, apiKey: 'k', version: 3, enabled: true,
    });

    const trigger = await api.jobs.trigger('QueueCleaner');
    expect(trigger.status).toBeLessThan(300);
    await new Promise((r) => setTimeout(r, 4_000));

    const deletes = await mocks.arr.findRequests({ method: 'DELETE', urlPattern: '/api/v3/queue/502.*' });
    expect(deletes).toEqual([]);

    await api.downloadClient.delete(clientId);
  });

  test('an unreachable client surfaces as not-found rather than a silent removal', async ({ api, mocks }) => {
    await ArrStubs.applyArrDefaults(mocks.arr);
    await mocks.downloadClient.stub(DownloadClientStubs.sabUnreachableStub());
    await mocks.arr.stub(
      ArrStubs.arrQueueStub([
        sabQueueRecord({ id: 503, title: 'sab.unreachable.job', status: 'downloading', downloadId: 'SAB-UNREACHABLE-1', protocol: 'usenet' }),
      ]),
    );

    const clientId = await registerSabClient(api);
    await api.arr.createInstance('sonarr', {
      name: 'sonarr-sab-qc-unreachable', url: TEST_CONFIG.mocks.arrUrl, apiKey: 'k', version: 3, enabled: true,
    });

    const trigger = await api.jobs.trigger('QueueCleaner');
    expect(trigger.status, 'an unreachable client must not fail the whole job run').toBeLessThan(300);
    await new Promise((r) => setTimeout(r, 4_000));

    const deletes = await mocks.arr.findRequests({ method: 'DELETE', urlPattern: '/api/v3/queue/503.*' });
    expect(deletes, 'an unreachable client must not be mistaken for "safe to remove"').toEqual([]);

    await api.downloadClient.delete(clientId);
  });

  test('a failed import missing from SABnzbd is removed when skip if not found is off', async ({ api, mocks }) => {
    test.setTimeout(120_000);
    const recordId = 504;

    await ArrStubs.applyArrDefaults(mocks.arr);
    await mocks.downloadClient.stub(DownloadClientStubs.sabQueueStub([]));
    await mocks.downloadClient.stub(DownloadClientStubs.sabHistoryStub([]));
    await mocks.arr.stub(
      ArrStubs.arrRawQueueStub(
        JSON.stringify({
          page: 1,
          pageSize: 50,
          totalRecords: 1,
          records: [
            {
              id: recordId,
              seriesId: 1,
              episodeId: 1,
              title: 'sab.missing.failed.import',
              status: 'completed',
              trackedDownloadStatus: 'warning',
              trackedDownloadState: 'importFailed',
              downloadId: 'SAB-MISSING-1',
              protocol: 'usenet',
              statusMessages: [{ title: 'sab.missing.failed.import', messages: ['Unable to import automatically'] }],
            },
          ],
        }),
      ),
    );

    await mocks.arr.stub(ArrStubs.arrQueueDeleteStub());

    const current = await (await api.queueCleaner.getConfig()).json();
    const qc = await api.queueCleaner.updateConfig({
      ...current,
      failedImport: {
        ...current.failedImport,
        maxStrikes: 3,
        skipIfNotFoundInClient: false,
        patternMode: 'Include',
        patterns: ['Unable to import automatically'],
      },
    });
    expect(qc.ok, `queue cleaner updateConfig: ${qc.status}`).toBe(true);

    const cfg = await api.arr.updateConfig('sonarr', { failedImportMaxStrikes: 1 });
    expect(cfg.ok, `arr updateConfig: ${cfg.status}`).toBe(true);

    const clientId = await registerSabClient(api);
    await api.arr.createInstance('sonarr', {
      name: 'sonarr-sab-qc-missing', url: TEST_CONFIG.mocks.arrUrl, apiKey: 'k', version: 3, enabled: true,
    });

    const trigger = await api.jobs.trigger('QueueCleaner');
    expect(trigger.status).toBeLessThan(300);

    await expect
      .poll(
        async () => (await mocks.arr.findRequests({ method: 'DELETE', urlPattern: `/api/v3/queue/${recordId}.*` })).length,
        { timeout: 60_000, intervals: [1_000] },
      )
      .toBeGreaterThan(0);

    await api.downloadClient.delete(clientId);
  });
});
