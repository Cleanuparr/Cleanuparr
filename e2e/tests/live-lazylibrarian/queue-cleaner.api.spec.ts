import { test, expect, TEST_CONFIG } from '../fixtures/base';
import type { CleanuparrApi } from '../helpers/api';
import { buildDownloadClientPayload } from '../helpers/api/download-client';
import {
  type LazyLibrarianBook,
  type SnatchedBook,
  liveLazyLibrarian,
  prepareRelease,
  qbittorrent,
  snatchBook,
  snatchPreparedRelease,
  torrentPresent,
  waitForTorrentInClient,
} from '../helpers/live-lazylibrarian';

/**
 * The Queue Cleaner against a real LazyLibrarian, on the shipped image.
 *
 * LazyLibrarian searches the fake Torznab indexer, grabs the torrent and hands it
 * to qBittorrent, so every wanted row here is one LazyLibrarian wrote itself.
 *
 * Each removal is asserted on both sides:
 *  - qBittorrent no longer holds the hash, which is the inline delete Cleanuparr does
 *  - LazyLibrarian reports the book as Wanted, which proves `queueBook` reached it
 *
 * Each spec claims a seeded book by index, so a retry cannot reuse another spec's
 * book. A retry starts a new worker, which resets any module counter.
 *
 * Only the stall rule is covered. The fixture torrent has no seeder, so it never
 * makes slow progress, and Cleanuparr classifies it as stalled rather than slow.
 * The slow rule stays with the stub-backed suite, which can set a speed.
 */

const REMOVAL_TIMEOUT = 60_000;
const RUN_SETTLE_MS = 8_000;

/** Enough runs to cover the strike limit plus the settling in between. */
const MAX_RUNS = 8;
const STRIKE_SETTLE_MS = 10_000;

/** The config rejects anything lower, so a removal needs three runs. */
const MIN_MAX_STRIKES = 3;

/**
 * Where the adopted torrent is filed before LazyLibrarian ever sees it.
 *
 * The data is not there, so the torrent stalls like every other one here.
 * The category stays empty, which is not LazyLibrarian's own label.
 */
const ADOPTED_SAVE_PATH = '/downloads/adopted';
const ADOPTED_CATEGORY = '';

/** Far enough out that the cron never fires inside a spec. */
function farFutureCron(): string {
  const minutes = (new Date().getUTCMinutes() + 30) % 60;
  return `0 ${minutes} * * * ?`;
}

let books: LazyLibrarianBook[] = [];
const createdRules: Array<{ kind: 'stall' | 'slow'; id: string }> = [];
let instanceId: string | undefined;
let clientId: string | undefined;

/** Hands out the seeded book a spec owns. */
function claimBook(index: number): LazyLibrarianBook {
  const book = books[index];

  if (!book) {
    throw new Error('the LazyLibrarian seed ran out of books, add more work ids to seed-lazylibrarian.sh');
  }

  return book;
}

async function createRule(
  api: CleanuparrApi,
  kind: 'stall' | 'slow',
  overrides: Record<string, unknown> = {},
): Promise<string> {
  const base = {
    name: `live-ll-${kind}-${Math.random().toString(36).slice(2, 8)}`,
    enabled: true,
    maxStrikes: MIN_MAX_STRIKES,
    privacyType: 'Both',
    minCompletionPercentage: 0,
    maxCompletionPercentage: 100,
    deletePrivateTorrentsFromClient: true,
    changeCategory: false,
    resetStrikesOnProgress: true,
  };

  const payload =
    kind === 'stall'
      ? { ...base, minimumProgress: null, ...overrides }
      : { ...base, minSpeed: '100KB', maxTimeHours: 0, ignoreAboveSize: null, ...overrides };

  const created = await (await api.queueCleaner.createRule(kind, payload)).json();
  expect(created.id, `createRule ${kind}`).toBeTruthy();
  createdRules.push({ kind, id: created.id });

  return created.id;
}

async function arrangeSnatch(bookIndex: number, options: Parameters<typeof snatchBook>[1] = {}): Promise<SnatchedBook> {
  const snatched = await snatchBook(claimBook(bookIndex), options);
  await waitForTorrentInClient(snatched.downloadId);
  return snatched;
}

/**
 * One strike lands per run, so the job runs until the strikes add up.
 *
 * The loop keeps going past the strike limit: a run has to finish before its
 * strike counts, and the removal is published on a message bus, so the exact
 * number of runs is not fixed.
 */
async function runUntilStruckOut(api: CleanuparrApi, downloadId: string): Promise<void> {
  for (let run = 0; run < MAX_RUNS; run++) {
    expect((await api.jobs.trigger('QueueCleaner')).status).toBeLessThan(300);
    await new Promise((r) => setTimeout(r, RUN_SETTLE_MS));

    if (run + 1 >= MIN_MAX_STRIKES && !(await torrentPresent(downloadId))) {
      return;
    }
  }
}

async function expectRemoved(snatched: SnatchedBook): Promise<void> {
  await expect
    .poll(() => torrentPresent(snatched.downloadId), { timeout: REMOVAL_TIMEOUT })
    .toBe(false);

  await expect
    .poll(() => liveLazyLibrarian.bookStatus(snatched.bookId), { timeout: REMOVAL_TIMEOUT })
    .toBe('Wanted');
}

/** The Queue Cleaner needs a client to evaluate the torrent and to delete it. */
async function createDownloadClient(api: CleanuparrApi): Promise<string> {
  const created = await (
    await api.downloadClient.create(
      buildDownloadClientPayload('qbittorrent', {
        name: 'live-lazylibrarian qbittorrent',
        host: qbittorrent.cleanuparrHost,
        username: qbittorrent.username,
        password: qbittorrent.password,
      }),
    )
  ).json();

  expect(created.id, 'createDownloadClient').toBeTruthy();
  return created.id;
}

test.describe.configure({ mode: 'serial' });

test.describe('QueueCleaner against a live LazyLibrarian', () => {
  test.beforeAll(async () => {
    await liveLazyLibrarian.waitReady();
    books = await liveLazyLibrarian.books();
    expect(books.length, 'the LazyLibrarian seed has no books').toBeGreaterThan(0);

    for (const book of books) {
      await liveLazyLibrarian.markSkipped(book.BookID);
    }
  });

  test.beforeEach(async ({ api }) => {
    const created = await (
      await api.arr.createInstance('lazylibrarian', {
        name: 'E2E live lazylibrarian',
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
  });

  test.afterEach(async ({ api }) => {
    for (const rule of createdRules.splice(0)) {
      await api.queueCleaner.deleteRule(rule.kind, rule.id);
    }

    if (instanceId) {
      await api.arr.deleteInstance('lazylibrarian', instanceId);
      instanceId = undefined;
    }

    if (clientId) {
      await api.downloadClient.delete(clientId);
      clientId = undefined;
    }

    await liveLazyLibrarian.clearHistory();
    await qbittorrent.clearAllTorrents();
  });

  test('a stall rule strikes the snatched book and removes it', async ({ api }) => {
    test.setTimeout(240_000);

    const snatched = await arrangeSnatch(0);
    await createRule(api, 'stall');

    await runUntilStruckOut(api, snatched.downloadId);

    await expectRemoved(snatched);
  });

  test('below the strike limit nothing is removed', async ({ api }) => {
    test.setTimeout(240_000);

    const snatched = await arrangeSnatch(1);
    await createRule(api, 'stall');

    expect((await api.jobs.trigger('QueueCleaner')).status).toBeLessThan(300);

    await new Promise((r) => setTimeout(r, STRIKE_SETTLE_MS));

    expect(await torrentPresent(snatched.downloadId), 'one strike must not remove the torrent').toBe(true);
    expect(await liveLazyLibrarian.bookStatus(snatched.bookId)).not.toBe('Wanted');
  });

  test('a private torrent stays in the client while the book still resets', async ({ api }) => {
    test.setTimeout(240_000);

    const snatched = await arrangeSnatch(2);
    await createRule(api, 'stall', { privacyType: 'Private', deletePrivateTorrentsFromClient: false });

    await runUntilStruckOut(api, snatched.downloadId);

    await expect
      .poll(() => liveLazyLibrarian.bookStatus(snatched.bookId), { timeout: REMOVAL_TIMEOUT })
      .toBe('Wanted');

    expect(await torrentPresent(snatched.downloadId), 'a private torrent must stay in the client').toBe(true);
  });

  test('the failed import path never strikes a LazyLibrarian item', async ({ api }) => {
    test.setTimeout(240_000);

    const snatched = await arrangeSnatch(3);

    const config = await (await api.queueCleaner.getConfig()).json();
    await api.queueCleaner.updateConfig({
      ...config,
      enabled: true,
      useAdvancedScheduling: true,
      cronExpression: farFutureCron(),
      failedImport: { ...config.failedImport, maxStrikes: 1 },
    });

    expect((await api.jobs.trigger('QueueCleaner')).status).toBeLessThan(300);

    await new Promise((r) => setTimeout(r, 10_000));

    expect(await torrentPresent(snatched.downloadId), 'LazyLibrarian handles failed imports itself').toBe(true);
    expect(await liveLazyLibrarian.bookStatus(snatched.bookId)).not.toBe('Wanted');
  });

  // LazyLibrarian records an adoption only when the client rejects a duplicate with a 409.
  // qBittorrent answers that from 5.2.0 on.
  test('an adopted torrent stays in the client while the book still resets', async ({ api }) => {
    test.setTimeout(240_000);

    const prepared = await prepareRelease(claimBook(12));

    // qBittorrent holds the torrent before LazyLibrarian grabs the release.
    // LazyLibrarian then adopts it instead of adding it.
    await qbittorrent.addStalledTorrent({
      metainfo: prepared.torrent.metainfo,
      savePath: ADOPTED_SAVE_PATH,
      category: ADOPTED_CATEGORY,
    });
    await waitForTorrentInClient(prepared.torrent.infoHash);

    const snatched = await snatchPreparedRelease(prepared);
    expect(snatched.origin, 'the client already held the torrent, so LazyLibrarian must adopt it').toBe('adopted');

    await createRule(api, 'stall');

    await runUntilStruckOut(api, snatched.downloadId);

    await expect
      .poll(() => liveLazyLibrarian.bookStatus(snatched.bookId), { timeout: REMOVAL_TIMEOUT })
      .toBe('Wanted');

    // Deleting it would stop a seed LazyLibrarian never started.
    expect(await torrentPresent(snatched.downloadId), 'an adopted torrent must stay in the client').toBe(true);
  });

  test('two snatched books are both handled in one run', async ({ api }) => {
    test.setTimeout(300_000);

    const first = await arrangeSnatch(4);
    const second = await arrangeSnatch(5);

    const rows = await liveLazyLibrarian.snatchedTorrents();
    expect(rows.length, 'LazyLibrarian should report both snatched torrents').toBe(2);

    await createRule(api, 'stall');

    await runUntilStruckOut(api, second.downloadId);

    await expectRemoved(first);
    await expectRemoved(second);
  });
});
