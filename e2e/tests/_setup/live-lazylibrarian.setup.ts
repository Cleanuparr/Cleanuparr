import { test as setup } from '@playwright/test';
import { restartAppAndWait } from '../helpers/test-lifecycle';
import { indexerMock, liveLazyLibrarian, qbittorrent } from '../helpers/live-lazylibrarian';

setup('reset app for live-lazylibrarian specs', async () => {
  await Promise.all([indexerMock.waitReady(), liveLazyLibrarian.waitReady(), qbittorrent.ready()]);
  await liveLazyLibrarian.clearHistory();
  await qbittorrent.clearAllTorrents();
  await restartAppAndWait();
});
