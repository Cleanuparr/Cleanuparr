import { test, expect, TEST_CONFIG } from '../fixtures/base';
import { ArrStubs } from '../helpers/mocks';

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
