import { test as setup } from '@playwright/test';
import { restartAppAndWait } from '../helpers/test-lifecycle';
import { indexerMock, liveLazyLibrarian, qbittorrent } from '../helpers/live-lazylibrarian';
import { SabnzbdDriver } from '../helpers/usenet-clients/sabnzbd';

const sabnzbd = new SabnzbdDriver();

setup('reset app for live-lazylibrarian specs', async () => {
  await Promise.all([indexerMock.waitReady(), liveLazyLibrarian.waitReady(), qbittorrent.ready(), sabnzbd.ready()]);
  await liveLazyLibrarian.clearHistory();
  await qbittorrent.clearAllTorrents();
  await sabnzbd.clearAll();
  await restartAppAndWait();
});
