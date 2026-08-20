import { resolve } from 'node:path';
import { TEST_CONFIG } from './test-config';
import { buildMultiFileTorrent, buildSingleFileTorrent, type GeneratedTorrent } from './torrent-fixtures';
import { QBittorrentDriver } from './torrent-clients/qbittorrent';
import { WireMockClient, type Mapping } from './mocks/wiremock-client';

/**
 * Direct access to the real LazyLibrarian container.
 *
 * The specs drive Cleanuparr through its own API, like every other spec.
 * This is the other side of the contract: what LazyLibrarian itself ended up with.
 *
 * Nothing here writes to lazylibrarian.db. A spec snatches through LazyLibrarian's
 * own search and download path, so the wanted row has the shape LazyLibrarian writes.
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
}

/**
 * Torznab ebook category.
 *
 * LazyLibrarian drops BOOKCAT from config.ini when it saves, so it asks for its
 * own default of 8000,8010. It does not filter the results by category itself.
 */
export const TORZNAB_BOOK_CATEGORY = 8010;

/**
 * Where the torrent payload is written.
 *
 * Deliberately outside the qBittorrent save path.
 * With no data on disk the grabbed torrent stalls and stays snatched.
 */
const TORRENT_SOURCE_DIR = resolve(__dirname, '..', '..', 'test-data', 'torznab-src');

const ADVERTISED_SIZE_BYTES = 2_147_483_648;

export class LiveLazyLibrarian {
  constructor(
    readonly url: string,
    private readonly apiKey: string,
  ) {}

  /**
   * Runs an API command and returns the body.
   *
   * Some commands answer with JSON and others with a bare string such as `OK`,
   * so the text comes back unparsed when it is not JSON.
   */
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

  /** Marks the book Wanted, which is what a search needs. */
  async markWanted(bookId: string): Promise<void> {
    await this.command('queueBook', { id: bookId });
  }

  /** Marks the book Skipped, which keeps LazyLibrarian's own searches off it. */
  async markSkipped(bookId: string): Promise<void> {
    await this.command('unqueueBook', { id: bookId });
  }

  searchBook(bookId: string): Promise<unknown> {
    return this.command('searchBook', { id: bookId, wait: '1' });
  }

  /**
   * Drops every wanted row, resets the snatched books to Wanted, and cancels the
   * download tasks in the client. This is LazyLibrarian's own history page action,
   * so no test has to touch its database.
   */
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
        // not up yet
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
 * LazyLibrarian tries several modes for one book, `t=search` among them, and it
 * discards the BOOKSEARCH setting when it saves its config. Matching the mode
 * would make this stub depend on which mode it picks, so it matches none.
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
}

/**
 * Drives a real snatch: LazyLibrarian searches, grabs and hands the torrent to
 * qBittorrent, then writes the wanted row itself.
 *
 * The release title carries the author and the book name so LazyLibrarian's
 * matcher accepts it.
 *
 * Both fixture builders set `private: 1`, so every torrent here is private.
 * A rule that has to act on one needs privacyType `Both` or `Private`, and a
 * removal from the client needs `deletePrivateTorrentsFromClient`.
 *
 * The torrent must contain a file whose extension is in LazyLibrarian's
 * EBOOK_TYPE, which defaults to epub, mobi and pdf. LazyLibrarian reads the
 * file list from the client and rejects a grab that has none, see
 * `download_client.py`: "has no eBook files".
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

  const torrent: GeneratedTorrent = options.files?.length
    ? buildMultiFileTorrent(TORRENT_SOURCE_DIR, releaseTitle, options.files)
    : buildSingleFileTorrent(TORRENT_SOURCE_DIR, `${releaseTitle}.epub`, 32_768, 'http://127.0.0.1:6969/announce');

  await indexerMock.stubMany([bookSearchStub(releaseTitle, file), torrentStub(file, torrent.metainfo)]);

  return { bookId: book.BookID, releaseTitle, torrent };
}

/** Runs the search and grab for a release whose stubs are already registered. */
export async function snatchPreparedRelease(prepared: PreparedRelease): Promise<SnatchedBook> {
  await liveLazyLibrarian.markWanted(prepared.bookId);
  await liveLazyLibrarian.searchBook(prepared.bookId);

  const row = await waitForSnatchedRow(prepared.bookId);

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
  timeoutMs = 90_000,
): Promise<LazyLibrarianHistoryRow> {
  const start = Date.now();
  let seen: LazyLibrarianHistoryRow[] = [];

  while (Date.now() - start < timeoutMs) {
    seen = await liveLazyLibrarian.history();
    const row = seen.find((r) => r.BookID === bookId && r.Status?.toLowerCase() === 'snatched' && !!r.DownloadID);

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

/** Far enough out that the cron never fires inside a spec. */
export function farFutureCron(): string {
  const minutes = (new Date().getUTCMinutes() + 30) % 60;
  return `0 ${minutes} * * * ?`;
}

/**
 * Hands out a seeded book by its work id.
 * Indexing into getAllBooks order would break: that query has no ORDER BY.
 */
export function claimBookById(books: LazyLibrarianBook[], bookId: string): LazyLibrarianBook {
  const book = books.find((candidate) => candidate.BookID === bookId);

  if (!book) {
    throw new Error(`the LazyLibrarian seed has no book ${bookId}, add its work id to seed-lazylibrarian.sh`);
  }

  return book;
}
