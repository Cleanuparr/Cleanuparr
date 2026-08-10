import { test, expect, TEST_CONFIG } from '../fixtures/base';
import { ArrStubs } from '../helpers/mocks';

const DOWNLOAD_ID = 'HASH-FRACTIONAL-SIZELEFT';
const RECORD_ID = 4242;

/**
 * A queue page from Sonarr v3.
 *
 * Each arr holds `sizeleft` in a decimal. Sonarr v3 writes its JSON with
 * Newtonsoft. A whole value then gets a decimal point. A client that reads this
 * field as a 64-bit integer cannot read the page, and one bad field stops the
 * full queue. The Queue Cleaner and the Seeker then do no work for that
 * instance.
 */
const QUEUE_BODY = `{
  "page": 1,
  "pageSize": 50,
  "totalRecords": 1,
  "records": [
    {
      "id": ${RECORD_ID},
      "seriesId": 7,
      "episodeId": 70,
      "title": "fractional.sizeleft.s01e01",
      "status": "completed",
      "trackedDownloadStatus": "warning",
      "trackedDownloadState": "importFailed",
      "downloadId": "${DOWNLOAD_ID}",
      "protocol": "torrent",
      "size": 4467066880.0,
      "sizeleft": 4467066880.0,
      "statusMessages": [
        { "title": "fractional.sizeleft.s01e01", "messages": ["Unable to import automatically"] }
      ]
    }
  ]
}`;

test.describe.serial('QueueCleaner — fractional sizeleft from Sonarr v3', () => {
  test('removes a failed import from a queue page that holds a fractional sizeleft', async ({ api, mocks }) => {
    test.setTimeout(120_000);

    // The body must keep the decimal point of sizeleft.
    expect(QUEUE_BODY).toContain('"sizeleft": 4467066880.0');

    await ArrStubs.applyArrDefaults(mocks.arr);
    await mocks.arr.stub(ArrStubs.arrRawQueueStub(QUEUE_BODY));

    // The failed import check needs a pattern that matches the status message.
    const current = await (await api.queueCleaner.getConfig()).json();
    const qc = await api.queueCleaner.updateConfig({
      ...current,
      failedImport: {
        ...current.failedImport,
        maxStrikes: 3,
        patternMode: 'Include',
        patterns: ['Unable to import automatically'],
      },
    });
    expect(qc.ok, `queue cleaner updateConfig: ${qc.status}`).toBe(true);

    // The arr value replaces the 3 strikes above, so one job run is enough.
    const cfg = await api.arr.updateConfig('sonarr', { failedImportMaxStrikes: 1 });
    expect(cfg.ok, `arr updateConfig: ${cfg.status}`).toBe(true);

    const created = await api.arr.createInstance('sonarr', {
      name: 'sonarr-fractional-sizeleft',
      url: TEST_CONFIG.mocks.arrUrl,
      apiKey: 'k',
      version: 3,
      enabled: true,
    });
    expect(created.ok, `createInstance: ${created.status}`).toBe(true);

    const trigger = await api.jobs.trigger('QueueCleaner');
    expect(trigger.status).toBeLessThan(300);

    await expect
      .poll(
        async () => {
          const requests = (await mocks.arr.findRequests({
            method: 'DELETE',
            urlPattern: '/api/v3/queue/.*',
          })) as unknown as Array<{ url: string }>;
          return requests.some((r) => r.url.includes(`/api/v3/queue/${RECORD_ID}`));
        },
        { timeout: 60_000, intervals: [1_000] },
      )
      .toBe(true);
  });
});
