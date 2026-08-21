import { test, expect } from '../fixtures/base';
import {
  arrangeLiveInstance,
  claimBookById,
  createStallRule,
  indexerMock,
  type LazyLibrarianBook,
  liveLazyLibrarian,
  pinQueueCleanerSchedule,
  qbittorrent,
  resetLibrary,
  runUntilStruckOut,
  setSearchEnabled,
  snatchBook,
  teardownLiveInstance,
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

    await teardownLiveInstance(api, { instanceId, clientId });
    instanceId = undefined;
    clientId = undefined;

    await resetLibrary(books);
    await qbittorrent.clearAllTorrents();
    await indexerMock.resetAll();
    await api.general.purgeStrikes();
  });

  test('the audiobook status is reset and the ebook status is left alone', async ({ api }) => {
    test.setTimeout(240_000);

    await setSearchEnabled(api, false);

    ({ instanceId, clientId } = await arrangeLiveInstance(api, { name: 'E2E live lazylibrarian audiobook' }));
    await pinQueueCleanerSchedule(api);

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
