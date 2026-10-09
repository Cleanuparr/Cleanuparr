import { test, expect } from '../fixtures/base';
import { adminTokens } from '../helpers/test-lifecycle';
import { findEventWithDeleteReason } from '../helpers/api/signalr';
import { SabnzbdDriver } from '../helpers/usenet-clients/sabnzbd';
import {
  claimBookById,
  createSabnzbdDownloadClient,
  type LazyLibrarianBook,
  liveLazyLibrarian,
  pinQueueCleanerSchedule,
  pointLazyLibrarianAtSabnzbd,
  resetLibrary,
  restoreLazyLibrarianConfig,
  setSearchEnabled,
  snatchBookViaSabnzbd,
  teardownLiveInstance,
} from '../helpers/live-lazylibrarian';

/**
 * LazyLibrarian snatching to SABnzbd, against a real LazyLibrarian and a real SABnzbd.
 *
 * The seeded LazyLibrarian only knows qBittorrent + a Torznab provider, so
 * `pointLazyLibrarianAtSabnzbd` stops the container, adds a SABnzbd downloader and a
 * Newznab provider to its config.ini, and starts it again: the same stop/edit/start
 * dance `scripts/seed-lazylibrarian.sh` uses to write the seed, since LazyLibrarian
 * only persists config on shutdown and only reads it on startup.
 *
 * The NZB SABnzbd receives references a missing sabnews article, so the job reaches
 * history as Failed fast, without needing a real multi-part download to complete.
 */

const REMOVAL_TIMEOUT = 60_000;
const EVENT_TIMEOUT = 30_000;

// An OpenLibrary work id from seed-lazylibrarian.sh that no other live-lazylibrarian spec claims.
const BOOK_ID = 'OL24034W';

const sabnzbd = new SabnzbdDriver();

let books: LazyLibrarianBook[] = [];
let instanceId: string | undefined;
let clientId: string | undefined;

test.describe.configure({ mode: 'serial' });

test.describe('LazyLibrarian snatching to a live SABnzbd', () => {
  test.beforeAll(async () => {
    test.setTimeout(180_000);

    await Promise.all([liveLazyLibrarian.waitReady(), sabnzbd.ready()]);
    books = await liveLazyLibrarian.books();
    expect(books.length, 'the LazyLibrarian seed has no books').toBeGreaterThan(0);

    await resetLibrary(books);
    await sabnzbd.clearAll();
    await pointLazyLibrarianAtSabnzbd(sabnzbd);
  });

  test.afterAll(async () => {
    test.setTimeout(60_000);
    await restoreLazyLibrarianConfig();
  });

  test.beforeEach(async ({ api }) => {
    await setSearchEnabled(api, false);

    const createdInstance = await (
      await api.arr.createInstance('lazylibrarian', {
        name: 'E2E live lazylibrarian sabnzbd',
        url: 'http://127.0.0.1:5299',
        apiKey: '0000000000000000000000000000e2e3',
        version: 1,
        enabled: true,
      })
    ).json();
    expect(createdInstance.id, 'createInstance').toBeTruthy();
    instanceId = createdInstance.id;

    clientId = await createSabnzbdDownloadClient(api, sabnzbd);
    await pinQueueCleanerSchedule(api);
  });

  test.afterEach(async ({ api }) => {
    await teardownLiveInstance(api, { instanceId, clientId });
    instanceId = undefined;
    clientId = undefined;

    await resetLibrary(books);
    await sabnzbd.clearAll();
    await api.general.purgeStrikes();
  });

  test('a failed SABnzbd job is removed and strikes DownloadFailed', async ({ api }) => {
    test.setTimeout(300_000);

    const book = claimBookById(books, BOOK_ID);
    const snatched = await snatchBookViaSabnzbd(book);

    // The core assumption this spec exists to check: LazyLibrarian records the SABnzbd
    // nzo_id as the DownloadID, the same way it records a torrent hash.
    const { queue, history } = await sabnzbd.findJob(snatched.downloadId);
    expect(queue?.nzo_id ?? history?.nzo_id, 'LazyLibrarian must record the SABnzbd nzo_id as the DownloadID').toBe(
      snatched.downloadId,
    );

    // A missing article can sit out SABnzbd's 60s news server timeout before the job fails.
    await sabnzbd.waitForHistoryStatus(snatched.downloadId, 'Failed', 120_000);

    expect((await api.jobs.trigger('QueueCleaner')).status).toBeLessThan(300);

    await expect
      .poll(async () => (await sabnzbd.findJob(snatched.downloadId)).history, { timeout: REMOVAL_TIMEOUT })
      .toBeUndefined();

    const token = adminTokens().accessToken;
    const event = await findEventWithDeleteReason(token, snatched.downloadId, 'DownloadFailed', EVENT_TIMEOUT);
    expect(event, 'no DownloadFailed event for the SABnzbd job').toBeTruthy();
  });
});
