import { test as setup, expect } from '@playwright/test';
import { restartAppAndWait, adminTokens } from '../helpers/test-lifecycle';
import { CleanuparrApi } from '../helpers/api';
import { indexerMock, liveRadarr, liveSonarr } from '../helpers/live-arr';
import { MANUAL_SEARCH_INTERVAL_MINUTES } from '../helpers/seeker-live';

setup('reset app for live-arr-fast specs', async () => {
  await Promise.all([indexerMock.waitReady(), liveSonarr.waitReady(), liveRadarr.waitReady()]);
  await restartAppAndWait();

  // Every spec in this folder triggers its own runs.
  // The schedule is pushed out of the way so a cron tick cannot run one too.
  const api = new CleanuparrApi({ token: adminTokens().accessToken });
  const config = await (await api.seeker.getConfig()).json();
  const updated = await api.seeker.updateConfig({
    ...config,
    searchInterval: MANUAL_SEARCH_INTERVAL_MINUTES,
  });

  expect(updated.ok, `Could not park the Seeker schedule: ${updated.status}`).toBe(true);
});
