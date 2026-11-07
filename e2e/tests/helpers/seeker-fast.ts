import { TEST_CONFIG } from './test-config';
import type { CleanuparrApi } from './api';
import type { MockServers } from './mocks/wiremock-client';
import { MANUAL_SEARCH_INTERVAL_MINUTES } from './seeker-live';
import {
  applyArrDefaults,
  arrCommandTriggerStub,
  arrMoviesStub,
  arrQualityProfilesStub,
  arrWantedStub,
} from './mocks/arr-stubs';

export const COMMAND_ID = 4242;

const createdInstanceIds: string[] = [];
let savedSearchSettings: Record<string, unknown> | undefined;

/** Creates a Radarr instance with one searchable movie, so one Seeker run sends one command. */
export async function arrangeSearchableInstance(
  api: CleanuparrApi,
  mocks: MockServers,
  movieTitle: string,
  commandStubs: Parameters<MockServers['arr']['stubMany']>[0],
  instanceOverrides: Record<string, unknown> = {},
): Promise<string> {
  await applyArrDefaults(mocks.arr);
  await mocks.arr.stubMany([
    arrMoviesStub([{ id: 1, title: movieTitle }]),
    arrWantedStub('missing', [{ id: 1 }]),
    arrWantedStub('cutoff', []),
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

  createdInstanceIds.push(instance.id);

  const config = await (await api.seeker.getConfig()).json();
  savedSearchSettings ??= {
    searchEnabled: config.searchEnabled,
    proactiveSearchEnabled: config.proactiveSearchEnabled,
    searchInterval: config.searchInterval,
  };

  // The arr mock stubs no grab history, so a window would skip the search.
  const instances = config.instances.map((i: { arrInstanceId: string }) =>
    i.arrInstanceId === instance.id
      ? { ...i, enabled: true, monitoredOnly: true, recentGrabWindowHours: 0, ...instanceOverrides }
      : i,
  );

  await api.seeker.updateConfig({
    ...config,
    instances,
    searchEnabled: true,
    proactiveSearchEnabled: true,
    searchInterval: MANUAL_SEARCH_INTERVAL_MINUTES,
  });

  return instance.id;
}

/** Removes the instances arrangeSearchableInstance made and restores the Seeker settings. */
export async function teardownSearchableInstances(api: CleanuparrApi): Promise<void> {
  for (const id of createdInstanceIds.splice(0)) {
    await api.arr.deleteInstance('radarr', id);
  }

  if (savedSearchSettings) {
    const config = await (await api.seeker.getConfig()).json();
    await api.seeker.updateConfig({ ...config, ...savedSearchSettings });
  }
}
