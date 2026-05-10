import { test, expect, TEST_CONFIG } from '../fixtures/base';
import { ArrStubs } from '../helpers/mocks';

test.describe('QueueCleaner — job execution end-to-end', () => {
  test('triggers a JobRun event when invoked manually', async ({ api, mocks }) => {
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

    const start = Date.now();
    let jobRunFound = false;
    while (Date.now() - start < 30_000) {
      const events = await (await api.events.list({
        eventType: 'JobRun',
        page: 1,
        pageSize: 10,
      })).json();
      const items = events.items ?? events.records ?? [];
      if (items.length > 0) {
        jobRunFound = true;
        break;
      }
      await new Promise((r) => setTimeout(r, 500));
    }
    expect(jobRunFound).toBe(true);
  });

  test('records a strike for a stalled queue item', async ({ api, mocks }) => {
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

    await api.queueCleaner.createRule('stall', {
      name: 'stall-rule-job',
      enabled: true,
      maxStrikes: 3,
      applyTo: 'all',
      stalledDuration: '00:00:00',
    });

    await api.jobs.trigger('QueueCleaner');

    const start = Date.now();
    let strikeRecorded = false;
    while (Date.now() - start < 30_000) {
      const list = await (await api.strikes.list({ page: 1, pageSize: 50 })).json();
      const items = list.items ?? list.records ?? [];
      if (items.length > 0) {
        strikeRecorded = true;
        break;
      }
      await new Promise((r) => setTimeout(r, 500));
    }
    expect(strikeRecorded).toBe(true);
  });
});
