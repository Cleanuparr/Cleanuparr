import { test, expect, TEST_CONFIG } from '../fixtures/base';
import type { CleanuparrApi } from '../helpers/api';
import { appLogsSince } from '../helpers/test-lifecycle';
import {
  arrangeLiveInstance,
  claimBookById,
  createStallRule,
  indexerMock,
  type LazyLibrarianBook,
  lazyLibrarianLogsSince,
  liveLazyLibrarian,
  MIN_MAX_STRIKES,
  pinQueueCleanerSchedule,
  qbittorrent,
  resetLibrary,
  runUntilStrikes,
  runUntilStruckOut,
  setSearchEnabled,
  snatchBook,
  type SnatchedBook,
  teardownLiveInstance,
  waitForTorrentInClient,
  wantedStatus,
} from '../helpers/live-lazylibrarian';

/**
 * The re-search after a removal, against a real LazyLibrarian.
 *
 * `queueBook` alone leaves the wanted row Snatched.
 * Every LazyLibrarian search command skips a book in that state.
 * `getDownloadProgress` clears it, once the client no longer holds the torrent.
 *
 * The gate logs "already marked snatched in wanted table".
 * Its absence is what proves the search ran.
 */

const REMOVAL_TIMEOUT = 60_000;
const GATE_LOG = 'already marked snatched in wanted table';

/** Shared with other files. Each file resets the library. */
const BOOK_IDS = {
  aborted: 'OL21177W',
  searched: 'OL66554W',
} as const;

let books: LazyLibrarianBook[] = [];
let instanceId: string | undefined;
let clientId: string | undefined;
let ruleId: string | undefined;

/** Torznab searches the indexer answered after the given epoch millisecond. */
async function searchesSince(sinceMs: number): Promise<string[]> {
  const logged = await indexerMock.requests();

  return logged
    .filter((entry) => entry.request.loggedDate >= sinceMs && entry.request.url.includes('q='))
    .map((entry) => decodeURIComponent(entry.request.url.replace(/\+/g, ' ')));
}

async function arrange(
  api: CleanuparrApi,
  bookId: string,
  searchEnabled: boolean,
): Promise<{ book: LazyLibrarianBook; snatched: SnatchedBook }> {
  await setSearchEnabled(api, searchEnabled);

  ({ instanceId, clientId } = await arrangeLiveInstance(api, { name: 'E2E live lazylibrarian seeker' }));
  await pinQueueCleanerSchedule(api);

  const book = claimBookById(books, bookId);
  const snatched = await snatchBook(book);
  await waitForTorrentInClient(snatched.downloadId);
  ruleId = await createStallRule(api);

  return { book, snatched };
}

test.describe.configure({ mode: 'serial' });

test.describe('the re-search after a LazyLibrarian removal', () => {
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

  // Search off, so the re-grab cannot put the row back to Snatched.
  test('the wanted row is aborted once the torrent is gone', async ({ api }) => {
    test.setTimeout(240_000);

    const { snatched } = await arrange(api, BOOK_IDS.aborted, false);

    await runUntilStruckOut(api, snatched.downloadId);

    await expect
      .poll(() => wantedStatus(snatched.bookId), { timeout: REMOVAL_TIMEOUT })
      .toBe('Aborted');

    expect(await liveLazyLibrarian.bookStatus(snatched.bookId)).toBe('Wanted');
  });

  test('the book is searched again instead of being skipped as snatched', async ({ api }) => {
    test.setTimeout(240_000);

    const { book, snatched } = await arrange(api, BOOK_IDS.searched, true);

    const startedAt = new Date().toISOString();
    const startedAtMs = Date.now();

    await runUntilStrikes(api, snatched.downloadId, MIN_MAX_STRIKES);

    await expect
      .poll(() => appLogsSince(startedAt), { timeout: REMOVAL_TIMEOUT })
      .toContain(`book search triggered | ${TEST_CONFIG.liveLazyLibrarian.url}/ | book id: ${book.BookID}`);

    await expect
      .poll(() => searchesSince(startedAtMs), { timeout: REMOVAL_TIMEOUT })
      .not.toEqual([]);

    const searches = await searchesSince(startedAtMs);
    expect(
      searches.every((url) => url.includes(book.BookName)),
      `a search went out for another book:\n${searches.join('\n')}`,
    ).toBe(true);

    expect(lazyLibrarianLogsSince(startedAt), 'the search must not be gated on the wanted row')
      .not.toContain(GATE_LOG);
  });
});
