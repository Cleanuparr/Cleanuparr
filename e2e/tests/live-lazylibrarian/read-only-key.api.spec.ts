import { test, expect, TEST_CONFIG } from '../fixtures/base';
import { appLogsSince } from '../helpers/test-lifecycle';
import {
  MIN_MAX_STRIKES,
  claimBookById,
  createDownloadClient,
  createStallRule,
  farFutureCron,
  indexerMock,
  type LazyLibrarianBook,
  liveLazyLibrarian,
  resetLibrary,
  qbittorrent,
  runUntilStrikes,
  setSearchEnabled,
  snatchBook,
  strikeCount,
  torrentPresent,
  waitForTorrentInClient,
} from '../helpers/live-lazylibrarian';

/**
 * LazyLibrarian's read-only api key, against the shipped image.
 *
 * `getVersion` and `getHistory` are permission level 0.
 * A read-only key therefore passes the connection test and fills the queue.
 * `queueBook` is level 1, and LazyLibrarian refuses it with HTTP 200.
 *
 * Cleanuparr must not report that reset as done.
 */

const BOOK_ID = 'OL21177W';
const REFUSAL = 'not available with read-only api access key';

let books: LazyLibrarianBook[] = [];
let instanceId: string | undefined;
let clientId: string | undefined;
let ruleId: string | undefined;

test.describe.configure({ mode: 'serial' });

test.describe('LazyLibrarian read-only api key', () => {
  test.beforeAll(async () => {
    await liveLazyLibrarian.waitReady();
    books = await liveLazyLibrarian.books();

    await resetLibrary(books);
  });

  test.afterEach(async ({ api }) => {
    if (ruleId) {
      await api.queueCleaner.deleteRule('stall', ruleId);
      ruleId = undefined;
    }

    if (instanceId) {
      await api.arr.deleteInstance('lazylibrarian', instanceId);
      instanceId = undefined;
    }

    if (clientId) {
      await api.downloadClient.delete(clientId);
      clientId = undefined;
    }

    await resetLibrary(books);
    await qbittorrent.clearAllTorrents();
    await indexerMock.resetAll();
    await api.general.purgeStrikes();
  });

  test('the read-only key passes the connection test', async ({ api }) => {
    const res = await api.arr.testInstance('lazylibrarian', {
      name: 'live-lazylibrarian ro conn',
      url: TEST_CONFIG.liveLazyLibrarian.url,
      apiKey: TEST_CONFIG.liveLazyLibrarian.readOnlyApiKey,
      version: 1,
    });

    expect(res.ok, await res.text()).toBe(true);
  });

  test('a refused reset is not reported as done', async ({ api }) => {
    test.setTimeout(240_000);

    await setSearchEnabled(api, false);

    const created = await (
      await api.arr.createInstance('lazylibrarian', {
        name: 'E2E live lazylibrarian read-only',
        url: TEST_CONFIG.liveLazyLibrarian.url,
        apiKey: TEST_CONFIG.liveLazyLibrarian.readOnlyApiKey,
        version: 1,
        enabled: true,
      })
    ).json();
    instanceId = created.id;
    clientId = await createDownloadClient(api);

    const cleanerConfig = await (await api.queueCleaner.getConfig()).json();
    await api.queueCleaner.updateConfig({
      ...cleanerConfig,
      enabled: true,
      useAdvancedScheduling: true,
      cronExpression: farFutureCron(),
    });

    const snatched = await snatchBook(claimBookById(books, BOOK_ID));
    await waitForTorrentInClient(snatched.downloadId);

    // The torrent stays, so the run reaches queueBook without deleting anything.
    ruleId = await createStallRule(api, {
      privacyType: 'Private',
      deletePrivateTorrentsFromClient: false,
    });

    const startedAt = new Date().toISOString();
    await runUntilStrikes(api, snatched.downloadId, MIN_MAX_STRIKES);

    expect(await strikeCount(api, snatched.downloadId), 'the run must have reached this item')
      .toBeGreaterThanOrEqual(MIN_MAX_STRIKES);

    await expect
      .poll(() => appLogsSince(startedAt), { timeout: 30_000 })
      .toContain(REFUSAL);

    expect(await liveLazyLibrarian.bookStatus(snatched.bookId), 'a refused queueBook must not reset the book')
      .toBe('Snatched');
    expect(await torrentPresent(snatched.downloadId), 'the rule keeps private torrents').toBe(true);
  });
});
