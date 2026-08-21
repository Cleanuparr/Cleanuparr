import { test, expect, TEST_CONFIG } from '../fixtures/base';
import {
  claimBookById,
  createDownloadClient,
  createStallRule,
  farFutureCron,
  indexerMock,
  type LazyLibrarianBook,
  liveLazyLibrarian,
  qbittorrent,
  resetLibrary,
  runUntilStruckOut,
  setSearchEnabled,
  snatchBook,
  torrentPresent,
  waitForTorrentInClient,
} from '../helpers/live-lazylibrarian';

/**
 * An audiobook removal, against a real LazyLibrarian.
 *
 * LazyLibrarian keeps `Status` for the ebook and `AudioStatus` for the audiobook.
 * `queueBook` moves `Status` unless it is given `type=AudioBook`.
 * Dropping that parameter resets the wrong column, and `Status` alone cannot tell.
 */

const REMOVAL_TIMEOUT = 60_000;

/** Shared with the queue cleaner file. Each file resets the library. */
const BOOK_ID = 'OL85892W';

let books: LazyLibrarianBook[] = [];
let instanceId: string | undefined;
let clientId: string | undefined;
let ruleId: string | undefined;

test.describe.configure({ mode: 'serial' });

test.describe('an audiobook removal against a live LazyLibrarian', () => {
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

  test('the audiobook status is reset and the ebook status is left alone', async ({ api }) => {
    test.setTimeout(240_000);

    await setSearchEnabled(api, false);

    const created = await (
      await api.arr.createInstance('lazylibrarian', {
        name: 'E2E live lazylibrarian audiobook',
        url: TEST_CONFIG.liveLazyLibrarian.url,
        apiKey: TEST_CONFIG.liveLazyLibrarian.apiKey,
        version: 1,
        enabled: true,
      })
    ).json();
    instanceId = created.id;
    clientId = await createDownloadClient(api);

    const config = await (await api.queueCleaner.getConfig()).json();
    await api.queueCleaner.updateConfig({
      ...config,
      enabled: true,
      useAdvancedScheduling: true,
      cronExpression: farFutureCron(),
    });

    const snatched = await snatchBook(claimBookById(books, BOOK_ID), { library: 'AudioBook' });
    await waitForTorrentInClient(snatched.downloadId);
    ruleId = await createStallRule(api);

    await runUntilStruckOut(api, snatched.downloadId);

    await expect
      .poll(() => torrentPresent(snatched.downloadId), { timeout: REMOVAL_TIMEOUT })
      .toBe(false);

    await expect
      .poll(() => liveLazyLibrarian.audioStatus(snatched.bookId), { timeout: REMOVAL_TIMEOUT })
      .toBe('Wanted');

    expect(
      await liveLazyLibrarian.bookStatus(snatched.bookId),
      'resetting the ebook means queueBook lost its type parameter',
    ).toBe('Skipped');
  });
});
