import { test, expect, TEST_CONFIG } from '../fixtures/base';
import type { CleanuparrApi } from '../helpers/api';
import { indexerMock } from '../helpers/live-arr';
import { NotificationStubs } from '../helpers/mocks';
import type { MockServers } from '../helpers/mocks/wiremock-client';
import {
  RADARR,
  arrangeInstance,
  listSearchEvents,
  resetLiveArrState,
  teardownInstances,
  triggerSeeker,
} from '../helpers/seeker-live';
import { torznabSearchStub } from '../helpers/mocks/torznab-stubs';

const SETTLE_MS = 20_000;
const TITLE = 'Seeker cycle completed';

async function cycleCompletedEventCount(api: CleanuparrApi): Promise<number> {
  const res = await api.events.list({ eventType: 'SeekerCycleCompleted', page: 1, pageSize: 500 });
  expect(res.status, 'events query failed').toBe(200);
  return ((await res.json()).items ?? []).length;
}

test.describe('Seeker cycle completed notification', () => {
  let providerId: string | undefined;

  test.beforeEach(async ({ mocks }) => {
    await resetLiveArrState();
    await indexerMock.stub(torznabSearchStub(RADARR.searchMode, []));
    await mocks.notify.stub(NotificationStubs.gotifyMessageStub());
  });

  test.afterEach(async ({ api }) => {
    if (providerId) {
      await api.notifications.delete(providerId);
      providerId = undefined;
    }
    await teardownInstances(api);
    await resetLiveArrState();
  });

  async function createProvider(api: CleanuparrApi, onSeekerCycleCompleted: boolean): Promise<void> {
    const created = await api.notifications.create('gotify', {
      name: `gotify-seeker-cycle-${Date.now()}`,
      serverUrl: TEST_CONFIG.mocks.notifyUrl,
      applicationToken: 'gotify-app-token',
      onSeekerCycleCompleted,
    });
    const body = await created.text();
    expect(created.status, body).toBeLessThan(300);
    providerId = JSON.parse(body).id as string;
  }

  // Resolves once the cycle is marked complete, which is when the notification would be sent.
  async function completeCycle(api: CleanuparrApi): Promise<string> {
    const instanceId = await arrangeInstance(api, RADARR, { instance: { minCycleTimeDays: 7 } });

    await triggerSeeker(api);
    await expect.poll(async () => (await listSearchEvents(api, instanceId)).length, { timeout: SETTLE_MS }).toBe(1);

    await triggerSeeker(api);
    await expect
      .poll(
        async () => {
          const res = await api.seeker.getSearchStatsSummary();
          const body = (await res.json()) as {
            perInstanceStats: Array<{ instanceId: string; cycleCompletedAt: string | null }>;
          };
          return body.perInstanceStats.find((s) => s.instanceId === instanceId)?.cycleCompletedAt ?? null;
        },
        { timeout: SETTLE_MS },
      )
      .not.toBeNull();

    return instanceId;
  }

  const cycleMessages = async (mocks: MockServers) =>
    (await mocks.notify.findRequests({ method: 'POST', urlPath: '/message' })).filter((r) =>
      (r.body ?? '').includes(TITLE),
    );

  test('notifies once when a cycle completes', async ({ api, mocks }) => {
    test.setTimeout(180_000);

    await createProvider(api, true);
    const eventsBefore = await cycleCompletedEventCount(api);

    await completeCycle(api);

    await expect.poll(async () => (await cycleMessages(mocks)).length, { timeout: 30_000 }).toBe(1);
    const sent = (await cycleMessages(mocks))[0];
    expect(sent.body).toContain('E2E live radarr');

    await expect.poll(() => cycleCompletedEventCount(api), { timeout: SETTLE_MS }).toBe(eventsBefore + 1);

    // Another run while the cycle waits out its minimum time must not notify again.
    await triggerSeeker(api);
    await new Promise((resolve) => setTimeout(resolve, SETTLE_MS));

    expect(await cycleMessages(mocks)).toHaveLength(1);
    expect(await cycleCompletedEventCount(api)).toBe(eventsBefore + 1);
  });

  test('stays silent when the cycle completed toggle is off', async ({ api, mocks }) => {
    test.setTimeout(180_000);

    await createProvider(api, false);
    const eventsBefore = await cycleCompletedEventCount(api);

    await completeCycle(api);

    await expect.poll(() => cycleCompletedEventCount(api), { timeout: SETTLE_MS }).toBe(eventsBefore + 1);

    // The notification queue sends after the event is saved, so give a send time to arrive.
    await new Promise((resolve) => setTimeout(resolve, SETTLE_MS));
    expect(await cycleMessages(mocks)).toHaveLength(0);
  });
});
