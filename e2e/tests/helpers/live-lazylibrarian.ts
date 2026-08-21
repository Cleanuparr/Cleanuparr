import { execSync } from 'node:child_process';
import { resolve } from 'node:path';
import { expect } from '@playwright/test';
import type { CleanuparrApi } from './api';
import { buildDownloadClientPayload } from './api/download-client';
import { TEST_CONFIG } from './test-config';
import { buildMultiFileTorrent, buildSingleFileTorrent, type GeneratedTorrent } from './torrent-fixtures';
import { QBittorrentDriver } from './torrent-clients/qbittorrent';
import { WireMockClient, type Mapping } from './mocks/wiremock-client';

/**
 * Direct access to the real LazyLibrarian container.
 *
 * Nothing here writes to lazylibrarian.db.
 * A spec snatches through LazyLibrarian's own search and download path.
 */

/** One row of the `wanted` table, as `getHistory` returns it. */
export interface LazyLibrarianHistoryRow {
  BookID: string;
  NZBtitle: string;
  NZBurl: string;
  Status: string;
  NZBmode: string;
  Source: string;
  DownloadID: string;
  AuxInfo: string;
  /** `new` when LazyLibrarian added the torrent, `adopted` when the client already held it. */
  Origin: string | null;
}

export interface LazyLibrarianBook {
  BookID: string;
  BookName: string;
  AuthorName: string;
  Status: string;
  /** Null until the audiobook has been asked for. */
  AudioStatus: string | null;
}

/** Which library a command applies to. */
export type BookLibrary = 'eBook' | 'AudioBook';

/**
 * Torznab ebook category.
 *
 * LazyLibrarian asks for its own default of 8000,8010 and never filters on it.
 */
export const TORZNAB_BOOK_CATEGORY = 8010;

/** Outside the qBittorrent save path, so the grabbed torrent stalls. */
const TORRENT_SOURCE_DIR = resolve(__dirname, '..', '..', 'test-data', 'torznab-src');

const ADVERTISED_SIZE_BYTES = 2_147_483_648;

export class LiveLazyLibrarian {
  constructor(
    readonly url: string,
    private readonly apiKey: string,
  ) {}

  /** Returns the parsed body, or the raw text when the command answers `OK`. */
  async command<T>(cmd: string, params: Record<string, string> = {}): Promise<T> {
    const query = new URLSearchParams({ apikey: this.apiKey, cmd, ...params });
    const res = await fetch(`${this.url}/api?${query}`);

    if (!res.ok) {
      throw new Error(`cmd=${cmd} on ${this.url} returned ${res.status}: ${await res.text()}`);
    }

    const text = await res.text();

    if (!text) {
      return null as T;
    }

    try {
      return JSON.parse(text) as T;
    } catch {
      return text as T;
    }
  }

  history(): Promise<LazyLibrarianHistoryRow[]> {
    return this.command<LazyLibrarianHistoryRow[]>('getHistory');
  }

  async books(): Promise<LazyLibrarianBook[]> {
    return this.command<LazyLibrarianBook[]>('getAllBooks');
  }

  async book(bookId: string): Promise<LazyLibrarianBook | undefined> {
    return (await this.books()).find((b) => b.BookID === bookId);
  }

  async bookStatus(bookId: string): Promise<string | undefined> {
    return (await this.book(bookId))?.Status;
  }

  /** The audiobook status, which lives in its own column. */
  async audioStatus(bookId: string): Promise<string | null | undefined> {
    return (await this.book(bookId))?.AudioStatus;
  }

  /** Marks the book Wanted, which is what a search needs. */
  async markWanted(bookId: string, library: BookLibrary = 'eBook'): Promise<void> {
    await this.command('queueBook', { id: bookId, ...(library === 'AudioBook' ? { type: library } : {}) });
  }

  /** Marks the book Skipped, which keeps LazyLibrarian's own searches off it. */
  async markSkipped(bookId: string, library: BookLibrary = 'eBook'): Promise<void> {
    await this.command('unqueueBook', { id: bookId, ...(library === 'AudioBook' ? { type: library } : {}) });
  }

  searchBook(bookId: string): Promise<unknown> {
    return this.command('searchBook', { id: bookId, wait: '1' });
  }

  /** LazyLibrarian's own history page action, so no test touches its database. */
  async clearHistory(): Promise<void> {
    const res = await fetch(`${this.url}/clearhistory?status=all`, { redirect: 'manual' });

    if (res.status >= 400) {
      throw new Error(`clearhistory on ${this.url} returned ${res.status}`);
    }
  }

  async waitReady(timeoutMs = 120_000): Promise<void> {
    const start = Date.now();
    while (Date.now() - start < timeoutMs) {
      try {
        const body = await this.command<{ Success?: boolean }>('getVersion');
        if (body?.Success) {
          return;
        }
      } catch {
        // Not up yet.
      }
      await new Promise((r) => setTimeout(r, 1_000));
    }

    throw new Error(`LazyLibrarian at ${this.url} did not become ready within ${timeoutMs}ms`);
  }
}

export const liveLazyLibrarian = new LiveLazyLibrarian(
  TEST_CONFIG.liveLazyLibrarian.url,
  TEST_CONFIG.liveLazyLibrarian.apiKey,
);

export const indexerMock = new WireMockClient(TEST_CONFIG.mocks.indexerAdminUrl);
export const qbittorrent = new QBittorrentDriver();

function bookFeed(title: string, file: string): string {
  return `<?xml version="1.0" encoding="UTF-8"?>
<rss version="2.0" xmlns:atom="http://www.w3.org/2005/Atom" xmlns:torznab="http://torznab.com/schemas/2015/feed">
  <channel>
    <title>E2E Indexer</title>
    <link>${TEST_CONFIG.mocks.indexerUrl}/</link>
    <description>E2E Indexer</description>
    <item>
      <title>${title}</title>
      <guid>${title}</guid>
      <link>${TEST_CONFIG.mocks.indexerUrl}/dl/${file}</link>
      <pubDate>Mon, 01 Jan 2024 00:00:00 +0000</pubDate>
      <size>${ADVERTISED_SIZE_BYTES}</size>
      <enclosure url="${TEST_CONFIG.mocks.indexerUrl}/dl/${file}" length="${ADVERTISED_SIZE_BYTES}" type="application/x-bittorrent" />
      <torznab:attr name="category" value="${TORZNAB_BOOK_CATEGORY}" />
      <torznab:attr name="seeders" value="20" />
      <torznab:attr name="peers" value="25" />
    </item>
  </channel>
</rss>`;
}

/**
 * Answers every search LazyLibrarian sends.
 *
 * LazyLibrarian tries several modes for one book, so this stub matches none.
 * The capabilities mapping keeps priority 1 and still answers `t=caps`.
 */
function bookSearchStub(title: string, file: string): Mapping {
  return {
    priority: 2,
    request: {
      method: 'GET',
      urlPath: '/api',
    },
    response: {
      status: 200,
      headers: { 'Content-Type': 'application/xml' },
      body: bookFeed(title, file),
    },
  };
}

function torrentStub(file: string, metainfo: Buffer): Mapping {
  return {
    request: { method: 'GET', urlPath: `/dl/${file}` },
    response: {
      status: 200,
      headers: { 'Content-Type': 'application/x-bittorrent' },
      base64Body: metainfo.toString('base64'),
    },
  };
}

export interface SnatchOptions {
  /** Files inside the torrent, for the Malware Blocker specs. */
  files?: Array<{ filename: string; sizeBytes: number }>;
  /** Which library to ask for. */
  library?: BookLibrary;
}

export interface SnatchedBook {
  bookId: string;
  releaseTitle: string;
  /** Lowercase infohash, which is what LazyLibrarian records as the download id. */
  downloadId: string;
  torrentName: string;
  /** The Origin LazyLibrarian recorded for the wanted row. */
  origin: string | null;
}

export interface PreparedRelease {
  bookId: string;
  releaseTitle: string;
  torrent: GeneratedTorrent;
  library: BookLibrary;
}

/**
 * Drives a real snatch through LazyLibrarian's own search and grab path.
 *
 * Every fixture torrent is private, so a rule needs privacyType Both or Private.
 * Removing one from the client needs `deletePrivateTorrentsFromClient`.
 * LazyLibrarian rejects a grab whose file list holds no epub, mobi or pdf.
 */
export async function snatchBook(
  book: LazyLibrarianBook,
  options: SnatchOptions = {},
): Promise<SnatchedBook> {
  return snatchPreparedRelease(await prepareRelease(book, options));
}

/**
 * Registers the Torznab stubs for a book and returns the torrent, unsnatched.
 *
 * A spec that needs the client to hold the torrent first adds it here.
 * It then calls `snatchPreparedRelease`.
 */
export async function prepareRelease(
  book: LazyLibrarianBook,
  options: SnatchOptions = {},
): Promise<PreparedRelease> {
  const releaseTitle = `${book.AuthorName} - ${book.BookName}`.replace(/[^\w\s.-]/g, ' ').trim();
  const file = `${book.BookID}.torrent`;
  const library = options.library ?? 'eBook';
  const extension = library === 'AudioBook' ? 'mp3' : 'epub';

  const torrent: GeneratedTorrent = options.files?.length
    ? buildMultiFileTorrent(TORRENT_SOURCE_DIR, releaseTitle, options.files)
    : buildSingleFileTorrent(TORRENT_SOURCE_DIR, `${releaseTitle}.${extension}`, 32_768, 'http://127.0.0.1:6969/announce');

  await indexerMock.stubMany([bookSearchStub(releaseTitle, file), torrentStub(file, torrent.metainfo)]);

  return { bookId: book.BookID, releaseTitle, torrent, library };
}

/** Runs the search and grab for a release whose stubs are already registered. */
export async function snatchPreparedRelease(prepared: PreparedRelease): Promise<SnatchedBook> {
  await liveLazyLibrarian.markWanted(prepared.bookId, prepared.library);
  await liveLazyLibrarian.searchBook(prepared.bookId);

  const row = await waitForSnatchedRow(prepared.bookId, prepared.library);

  return {
    bookId: prepared.bookId,
    releaseTitle: prepared.releaseTitle,
    downloadId: row.DownloadID.toLowerCase(),
    torrentName: prepared.torrent.name,
    origin: row.Origin,
  };
}

/** LazyLibrarian writes the download id once the client has accepted the torrent. */
export async function waitForSnatchedRow(
  bookId: string,
  library: BookLibrary = 'eBook',
  timeoutMs = 90_000,
): Promise<LazyLibrarianHistoryRow> {
  const start = Date.now();
  let seen: LazyLibrarianHistoryRow[] = [];

  while (Date.now() - start < timeoutMs) {
    seen = await liveLazyLibrarian.history();
    const row = seen.find(
      (r) => r.BookID === bookId && r.AuxInfo === library && r.Status?.toLowerCase() === 'snatched' && !!r.DownloadID,
    );

    if (row) {
      return row;
    }

    await new Promise((r) => setTimeout(r, 1_000));
  }

  throw new Error(
    `LazyLibrarian did not snatch ${bookId} within ${timeoutMs}ms, history: ${JSON.stringify(seen)}`,
  );
}

export async function waitForTorrentInClient(downloadId: string, timeoutMs = 30_000): Promise<void> {
  const target = downloadId.toLowerCase();
  const start = Date.now();

  while (Date.now() - start < timeoutMs) {
    const torrents = await qbittorrent.listTorrents();
    if (torrents.some((t) => t.hash.toLowerCase() === target)) {
      return;
    }
    await new Promise((r) => setTimeout(r, 500));
  }

  throw new Error(`qBittorrent never received ${downloadId}`);
}

export async function torrentPresent(downloadId: string): Promise<boolean> {
  const target = downloadId.toLowerCase();
  const torrents = await qbittorrent.listTorrents();
  return torrents.some((t) => t.hash.toLowerCase() === target);
}

/** The status of the `wanted` row for a snatch, undefined once the row is gone. */
export async function wantedStatus(
  bookId: string,
  library: BookLibrary = 'eBook',
): Promise<string | undefined> {
  const rows = await liveLazyLibrarian.history();
  return rows.find((row) => row.BookID === bookId && row.AuxInfo === library)?.Status;
}

/** A refused re-search shows up only in LazyLibrarian's own log. */
export function lazyLibrarianLogsSince(since: string): string {
  return execSync(
    `docker compose -f docker-compose.e2e.yml logs --since ${since} --no-log-prefix lazylibrarian`,
    { encoding: 'utf8', env: process.env, stdio: ['ignore', 'pipe', 'ignore'] },
  );
}

/**
 * Sets the master search toggle.
 *
 * The stub indexer answers a re-search with the same release.
 * LazyLibrarian then re-grabs the identical torrent within a second.
 */
export async function setSearchEnabled(api: CleanuparrApi, searchEnabled: boolean): Promise<void> {
  const config = await (await api.seeker.getConfig()).json();
  const res = await api.seeker.updateConfig({ ...config, searchEnabled });

  if (!res.ok) {
    throw new Error(`Seeker config update failed: ${await res.text()}`);
  }
}

/**
 * Sets the log level.
 *
 * A job names the item it is working on at Debug, and nowhere else.
 */
export async function setLogLevel(api: CleanuparrApi, level: string): Promise<void> {
  const config = await api.general.getJsonConfig();
  const res = await api.general.updateConfig({ ...config, log: { ...(config.log as object), level } });

  if (!res.ok) {
    throw new Error(`General config update failed: ${await res.text()}`);
  }
}

/**
 * Drops the history and puts every book back to Skipped.
 *
 * `clearhistory` resets a snatched book to Wanted.
 * LazyLibrarian runs its own backlog search a minute after the container starts.
 */
export async function resetLibrary(books: LazyLibrarianBook[]): Promise<void> {
  await liveLazyLibrarian.clearHistory();

  for (const book of books) {
    await liveLazyLibrarian.markSkipped(book.BookID);
    await liveLazyLibrarian.markSkipped(book.BookID, 'AudioBook');
  }
}

/** The config rejects anything lower, so a removal needs three runs. */
export const MIN_MAX_STRIKES = 3;

const RUN_SETTLE_MS = 8_000;
const MAX_RUNS = 8;

/** Cleanuparr needs a client to evaluate the torrent and to delete it. */
export async function createDownloadClient(api: CleanuparrApi): Promise<string> {
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

export async function createStallRule(
  api: CleanuparrApi,
  overrides: Record<string, unknown> = {},
): Promise<string> {
  const payload = {
    name: `live-ll-stall-${Math.random().toString(36).slice(2, 8)}`,
    enabled: true,
    maxStrikes: MIN_MAX_STRIKES,
    privacyType: 'Both',
    minCompletionPercentage: 0,
    maxCompletionPercentage: 100,
    deletePrivateTorrentsFromClient: true,
    changeCategory: false,
    resetStrikesOnProgress: true,
    minimumProgress: null,
    ...overrides,
  };

  const created = await (await api.queueCleaner.createRule('stall', payload)).json();
  expect(created.id, 'createRule stall').toBeTruthy();

  return created.id;
}

/**
 * The number of strikes recorded against a hash.
 * The endpoint groups them per download.
 */
export async function strikeCount(api: CleanuparrApi, downloadId: string): Promise<number> {
  const response = await api.strikes.list({ pageSize: 200 });
  expect(response.status).toBeLessThan(300);

  const body = await response.json();
  const rows: Array<Record<string, unknown>> = body.items ?? body.data ?? body ?? [];

  return rows
    .filter((row) => String(row.downloadId ?? '').toLowerCase() === downloadId.toLowerCase())
    .reduce((total, row) => total + Number(row.totalStrikes ?? 0), 0);
}

/**
 * Triggers the Queue Cleaner until the torrent leaves the client.
 *
 * A run has to finish before its strike counts.
 * The removal also goes through a message bus, so the run count is not fixed.
 */
export async function runUntilStruckOut(api: CleanuparrApi, downloadId: string): Promise<void> {
  for (let run = 0; run < MAX_RUNS; run++) {
    expect((await api.jobs.trigger('QueueCleaner')).status).toBeLessThan(300);
    await new Promise((r) => setTimeout(r, RUN_SETTLE_MS));

    if (run + 1 >= MIN_MAX_STRIKES && !(await torrentPresent(downloadId))) {
      return;
    }
  }
}

/**
 * Triggers the Queue Cleaner until the strike count is reached.
 *
 * Use it for a rule that keeps the torrent, where absence proves nothing.
 */
export async function runUntilStrikes(api: CleanuparrApi, downloadId: string, strikes: number): Promise<void> {
  for (let run = 0; run < MAX_RUNS; run++) {
    expect((await api.jobs.trigger('QueueCleaner')).status).toBeLessThan(300);
    await new Promise((r) => setTimeout(r, RUN_SETTLE_MS));

    if (await strikeCount(api, downloadId) >= strikes) {
      return;
    }
  }
}

/** Far enough out that the cron never fires inside a spec. */
export function farFutureCron(): string {
  const minutes = (new Date().getUTCMinutes() + 30) % 60;
  return `0 ${minutes} * * * ?`;
}

/** getAllBooks has no ORDER BY, so a spec must claim by work id. */
export function claimBookById(books: LazyLibrarianBook[], bookId: string): LazyLibrarianBook {
  const book = books.find((candidate) => candidate.BookID === bookId);

  if (!book) {
    throw new Error(`the LazyLibrarian seed has no book ${bookId}, add its work id to seed-lazylibrarian.sh`);
  }

  return book;
}
