import { test, expect, TEST_CONFIG } from '../fixtures/base';
import type { CleanuparrApi } from '../helpers/api';
import type { MockServers } from '../helpers/mocks/wiremock-client';
import {
  applyArrDefaults,
  arrCommandListStub,
  arrCommandNotFoundStub,
  arrCommandTriggerStub,
  arrMoviesStub,
  arrQualityProfilesStub,
} from '../helpers/mocks/arr-stubs';

const COMMAND_ID = 4242;

/** The monitor polls once a minute and the Seeker adds up to 30s of jitter. */
const TRANSITION_TIMEOUT = 180_000;

interface SearchEvent {
  id: string;
  itemTitle: string;
  searchStatus: string | null;
  isDryRun: boolean;
}

async function listSearchEvents(api: CleanuparrApi): Promise<SearchEvent[]> {
  const res = await api.seeker.getSearchEvents({ page: '1', pageSize: '50' });
  const body = await res.json();
  return body.items ?? body.records ?? body;
}

async function findSearchEvent(api: CleanuparrApi, title: string): Promise<SearchEvent | undefined> {
  const events = await listSearchEvents(api);
  return events.find((event) => event.itemTitle === title);
}

/** Creates a Radarr instance with one searchable movie, so one Seeker run sends one command. */
async function arrangeSearchableInstance(
  api: CleanuparrApi,
  mocks: MockServers,
  movieTitle: string,
  commandStubs: Parameters<MockServers['arr']['stubMany']>[0],
): Promise<string> {
  await applyArrDefaults(mocks.arr);
  await mocks.arr.stubMany([
    arrMoviesStub([{ id: 1, title: movieTitle }]),
    arrQualityProfilesStub(),
    arrCommandTriggerStub(COMMAND_ID),
    ...commandStubs,
  ]);

  const instance = await (
    await api.arr.createInstance('radarr', {
      name: 'E2E Radarr search flow',
      url: TEST_CONFIG.mocks.arrUrl,
      apiKey: 'e2e-test-key-radarr',
      version: 5,
    })
  ).json();

  const config = await (await api.seeker.getConfig()).json();
  const instances = config.instances.map((i: { arrInstanceId: string }) =>
    i.arrInstanceId === instance.id ? { ...i, enabled: true, monitoredOnly: true } : i,
  );

  await api.seeker.updateConfig({
    ...config,
    instances,
    searchEnabled: true,
    proactiveSearchEnabled: true,
    searchInterval: 2,
  });

  return instance.id;
}

async function expectSearchStatus(api: CleanuparrApi, title: string, status: string | null): Promise<void> {
  await expect
    .poll(async () => (await findSearchEvent(api, title))?.searchStatus, { timeout: TRANSITION_TIMEOUT })
    .toBe(status);
}

test.describe('Seeker: search command status flow', () => {
  test('reaches completed when the arr reports the command completed', async ({ api, mocks }) => {
    test.setTimeout(TRANSITION_TIMEOUT + 60_000);

    const title = 'Command Completes';
    await arrangeSearchableInstance(api, mocks, title, [
      arrCommandListStub([{ id: COMMAND_ID, status: 'completed' }]),
    ]);

    await api.jobs.trigger('Seeker');

    await expectSearchStatus(api, title, 'Completed');
  });

  test('reaches failed when the arr aborts the command', async ({ api, mocks }) => {
    test.setTimeout(TRANSITION_TIMEOUT + 60_000);

    const title = 'Command Aborts';
    await arrangeSearchableInstance(api, mocks, title, [
      arrCommandListStub([{ id: COMMAND_ID, status: 'aborted' }]),
    ]);

    await api.jobs.trigger('Seeker');

    await expectSearchStatus(api, title, 'Failed');
  });

  test('reaches completed when the arr has forgotten the command', async ({ api, mocks }) => {
    test.setTimeout(TRANSITION_TIMEOUT + 60_000);

    const title = 'Command Forgotten';
    await arrangeSearchableInstance(api, mocks, title, [
      arrCommandListStub([]),
      arrCommandNotFoundStub(COMMAND_ID),
    ]);

    await api.jobs.trigger('Seeker');

    await expectSearchStatus(api, title, 'Completed');
  });

  test('fails the in flight event when the arr instance is deleted', async ({ api, mocks }) => {
    test.setTimeout(120_000);

    const title = 'Instance Deleted';
    const instanceId = await arrangeSearchableInstance(api, mocks, title, [
      arrCommandListStub([{ id: COMMAND_ID, status: 'started' }]),
    ]);

    await api.jobs.trigger('Seeker');

    await expect
      .poll(async () => (await findSearchEvent(api, title))?.searchStatus, { timeout: 90_000 })
      .not.toBe(undefined);

    await api.arr.deleteInstance('radarr', instanceId);

    await expectSearchStatus(api, title, 'Failed');
  });

  test('leaves dry run search events without a status', async ({ api, mocks }) => {
    test.setTimeout(120_000);

    const title = 'Dry Run Search';
    await arrangeSearchableInstance(api, mocks, title, [
      arrCommandListStub([{ id: COMMAND_ID, status: 'completed' }]),
    ]);

    const general = await (await api.general.getConfig()).json();
    await api.general.updateConfig({ ...general, dryRun: true });

    try {
      await api.jobs.trigger('Seeker');

      await expect
        .poll(async () => (await findSearchEvent(api, title))?.isDryRun, { timeout: 90_000 })
        .toBe(true);

      const event = await findSearchEvent(api, title);
      expect(event?.searchStatus ?? null).toBeNull();
    } finally {
      await api.general.updateConfig({ ...general, dryRun: false });
    }
  });
});
