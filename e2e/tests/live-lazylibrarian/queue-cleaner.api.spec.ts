import { test, expect, farFutureCron } from '../fixtures/base';
import type { CleanuparrApi } from '../helpers/api';
import {
  arrangeLiveInstance,
  claimBookById,
  createStallRule,
  indexerMock,
  type LazyLibrarianBook,
  liveLazyLibrarian,
  MIN_MAX_STRIKES,
  pinQueueCleanerSchedule,
  prepareRelease,
  qbittorrent,
  resetLibrary,
  runUntilStrikes,
  runUntilStruckOut,
  setSearchEnabled,
  snatchBook,
  type SnatchedBook,
  snatchPreparedRelease,
  strikeCount,
  teardownLiveInstance,
  torrentPresent,
  waitForTorrentInClient,
} from '../helpers/live-lazylibrarian';

/**
 * The Queue Cleaner against a real LazyLibrarian, on the shipped image.
 *
 * Every wanted row here is one LazyLibrarian wrote itself.
 * Each removal is asserted on both sides:
 *  - qBittorrent no longer holds the hash, which is the inline delete
 *  - LazyLibrarian reports the book as Wanted, which proves `queueBook` reached it
 *
 * The re-search is off here, and owned by seeker.api.spec.ts.
 * Only the stall rule is covered, since the fixture torrent never progresses.
 */

const REMOVAL_TIMEOUT = 60_000;
const STRIKE_SETTLE_MS = 10_000;

/** Neither LazyLibrarian's own save path nor its own label. */
const ADOPTED_SAVE_PATH = '/downloads/adopted';
const ADOPTED_CATEGORY = '';

let books: LazyLibrarianBook[] = [];
const createdRules: Array<{ kind: 'stall'; id: string }> = [];
let instanceId: string | undefined;
let clientId: string | undefined;
let savedCleanerConfig: Record<string, unknown> | undefined;

/** One OpenLibrary work id per spec, from seed-lazylibrarian.sh. */
const BOOK_IDS = {
  stall: 'OL450063W',
  belowLimit: 'OL450124W',
  private: 'OL85892W',
  failedImport: 'OL52267W',
  firstOfTwo: 'OL52114W',
  secondOfTwo: 'OL52266W',
  adopted: 'OL66513W',
} as const;

function claimBook(bookId: string): LazyLibrarianBook {
  return claimBookById(books, bookId);
}

async function createRule(api: CleanuparrApi, overrides: Record<string, unknown> = {}): Promise<string> {
  const id = await createStallRule(api, overrides);
  createdRules.push({ kind: 'stall', id });

  return id;
}

async function arrangeSnatch(bookId: string, options: Parameters<typeof snatchBook>[1] = {}): Promise<SnatchedBook> {
  const snatched = await snatchBook(claimBook(bookId), options);
  await waitForTorrentInClient(snatched.downloadId);
  return snatched;
}

async function expectRemoved(snatched: SnatchedBook): Promise<void> {
  await expect
    .poll(() => torrentPresent(snatched.downloadId), { timeout: REMOVAL_TIMEOUT })
    .toBe(false);

  await expect
    .poll(() => liveLazyLibrarian.bookStatus(snatched.bookId), { timeout: REMOVAL_TIMEOUT })
    .toBe('Wanted');
}

test.describe.configure({ mode: 'serial' });

test.describe('QueueCleaner against a live LazyLibrarian', () => {
  test.beforeAll(async () => {
    await liveLazyLibrarian.waitReady();
    books = await liveLazyLibrarian.books();
    expect(books.length, 'the LazyLibrarian seed has no books').toBeGreaterThan(0);

    await resetLibrary(books);
  });

  test.beforeEach(async ({ api }) => {
    await setSearchEnabled(api, false);

    ({ instanceId, clientId } = await arrangeLiveInstance(api));
    await pinQueueCleanerSchedule(api);
  });

  test.afterEach(async ({ api }) => {
    for (const rule of createdRules.splice(0)) {
      await api.queueCleaner.deleteRule(rule.kind, rule.id);
    }

    await teardownLiveInstance(api, { instanceId, clientId });
    instanceId = undefined;
    clientId = undefined;

    if (savedCleanerConfig) {
      await api.queueCleaner.updateConfig(savedCleanerConfig);
      savedCleanerConfig = undefined;
    }

    await resetLibrary(books);
    await qbittorrent.clearAllTorrents();
    await indexerMock.resetAll();
    await api.general.purgeStrikes();
  });

  test('a stall rule strikes the snatched book and removes it', async ({ api }) => {
    test.setTimeout(240_000);

    const snatched = await arrangeSnatch(BOOK_IDS.stall);
    await createRule(api);

    await runUntilStruckOut(api, snatched.downloadId);

    await expectRemoved(snatched);
  });

  test('below the strike limit nothing is removed', async ({ api }) => {
    test.setTimeout(240_000);

    const snatched = await arrangeSnatch(BOOK_IDS.belowLimit);
    await createRule(api);

    // A freshly added torrent is still checking, so the first run does not strike.
    await runUntilStrikes(api, snatched.downloadId, 1);

    const strikes = await strikeCount(api, snatched.downloadId);
    expect(strikes, 'the run must have struck this item').toBeGreaterThan(0);
    expect(strikes, 'the strike limit must not be reached').toBeLessThan(MIN_MAX_STRIKES);
    expect(await torrentPresent(snatched.downloadId), 'one strike must not remove the torrent').toBe(true);
    expect(await liveLazyLibrarian.bookStatus(snatched.bookId)).toBe('Snatched');
  });

  test('a private torrent stays in the client while the book still resets', async ({ api }) => {
    test.setTimeout(240_000);

    const snatched = await arrangeSnatch(BOOK_IDS.private);
    await createRule(api, { privacyType: 'Private', deletePrivateTorrentsFromClient: false });

    await runUntilStrikes(api, snatched.downloadId, MIN_MAX_STRIKES);

    await expect
      .poll(() => liveLazyLibrarian.bookStatus(snatched.bookId), { timeout: REMOVAL_TIMEOUT })
      .toBe('Wanted');

    expect(await torrentPresent(snatched.downloadId), 'a private torrent must stay in the client').toBe(true);
    expect(await strikeCount(api, snatched.downloadId), 'the run must have reached this item').toBeGreaterThan(0);
  });

  test('the failed import path never strikes a LazyLibrarian item', async ({ api }) => {
    test.setTimeout(240_000);

    const snatched = await arrangeSnatch(BOOK_IDS.failedImport);

    const config = await (await api.queueCleaner.getConfig()).json();
    savedCleanerConfig = config;
    await api.queueCleaner.updateConfig({
      ...config,
      enabled: true,
      useAdvancedScheduling: true,
      cronExpression: farFutureCron(),
      failedImport: { ...config.failedImport, maxStrikes: 1 },
    });

    expect((await api.jobs.trigger('QueueCleaner')).status).toBeLessThan(300);

    await new Promise((resolve) => setTimeout(resolve, STRIKE_SETTLE_MS));

    // The stall rule is absent, so a strike here could only come from the failed-import path.
    expect(
      await strikeCount(api, snatched.downloadId),
      'the failed-import path must not strike a LazyLibrarian item',
    ).toBe(0);

    expect(await torrentPresent(snatched.downloadId), 'LazyLibrarian handles failed imports itself').toBe(true);
    expect(await liveLazyLibrarian.bookStatus(snatched.bookId)).toBe('Snatched');
  });

  // LazyLibrarian records an adoption only when the client rejects a duplicate add.
  test('an adopted torrent stays in the client while the book still resets', async ({ api }) => {
    test.setTimeout(240_000);

    const prepared = await prepareRelease(claimBook(BOOK_IDS.adopted));

    // The client holds the torrent before LazyLibrarian grabs the release.
    await qbittorrent.addStalledTorrent({
      metainfo: prepared.torrent.metainfo,
      savePath: ADOPTED_SAVE_PATH,
      category: ADOPTED_CATEGORY,
    });
    await waitForTorrentInClient(prepared.torrent.infoHash);

    const snatched = await snatchPreparedRelease(prepared);
    expect(snatched.origin, 'the client already held the torrent, so LazyLibrarian must adopt it').toBe('adopted');

    await createRule(api);

    await runUntilStrikes(api, snatched.downloadId, MIN_MAX_STRIKES);

    await expect
      .poll(() => liveLazyLibrarian.bookStatus(snatched.bookId), { timeout: REMOVAL_TIMEOUT })
      .toBe('Wanted');

    // Deleting it would stop a seed LazyLibrarian never started.
    expect(await torrentPresent(snatched.downloadId), 'an adopted torrent must stay in the client').toBe(true);
  });

  test('two snatched books are both handled in one run', async ({ api }) => {
    test.setTimeout(300_000);

    const first = await arrangeSnatch(BOOK_IDS.firstOfTwo);
    const second = await arrangeSnatch(BOOK_IDS.secondOfTwo);

    await createRule(api);

    await runUntilStruckOut(api, second.downloadId);

    await expectRemoved(first);
    await expectRemoved(second);
  });
});
