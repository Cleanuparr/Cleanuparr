import { test as setup } from '@playwright/test';
import { restartAppAndWait } from '../helpers/test-lifecycle';
import { indexerMock, liveRadarr, liveSonarr } from '../helpers/live-arr';

setup('reset app for live-arr specs', async () => {
  await Promise.all([indexerMock.waitReady(), liveSonarr.waitReady(), liveRadarr.waitReady()]);
  await restartAppAndWait();
});
