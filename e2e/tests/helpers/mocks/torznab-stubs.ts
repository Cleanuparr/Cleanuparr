import { resolve } from 'node:path';
import { buildSingleFileTorrent } from '../torrent-fixtures';
import { TEST_CONFIG } from '../test-config';
import type { Mapping } from './wiremock-client';

/**
 * Stubs for the fake Torznab indexer that the real Sonarr and Radarr containers
 * search against.
 *
 * The caps document and a probe result feed are file-based mappings under
 * `e2e/wiremock-indexer`, so the arrs can always validate the indexer even
 * before a test registers anything. A test adds a search stub for the release
 * it wants grabbed, plus the torrent that release points at.
 */

/** Torznab search mode. Sonarr sends `tvsearch`, Radarr sends `movie`. */
export type TorznabSearchMode = 'tvsearch' | 'movie' | 'search';

/** Torznab category. The seeded arrs are configured for the parent categories. */
export const TORZNAB_TV_CATEGORY = 5040;
export const TORZNAB_MOVIE_CATEGORY = 2040;

/**
 * The advertised release size.
 *
 * The arrs reject a release that is far smaller than the quality definition
 * allows, and the generated torrent is a few kilobytes, so the feed advertises a
 * plausible size instead. The arrs decide on this number and never compare it
 * with the torrent.
 */
const ADVERTISED_SIZE_BYTES = 2_147_483_648;

/**
 * Where the torrent payload is written.
 *
 * Deliberately not under the qBittorrent save path: with no data on disk the
 * grabbed torrent stalls instead of completing, so the arr keeps it in the queue
 * for the test to assert on.
 */
const TORRENT_SOURCE_DIR = resolve(__dirname, '..', '..', '..', 'test-data', 'torznab-src');

export interface TorznabRelease {
  title: string;
  category: number;
  /** Path segment the release's download link points at, under /dl. */
  file: string;
}

function feed(releases: TorznabRelease[]): string {
  const items = releases
    .map(
      (release) => `
    <item>
      <title>${release.title}</title>
      <guid>${release.title}</guid>
      <link>${TEST_CONFIG.mocks.indexerUrl}/dl/${release.file}</link>
      <pubDate>Mon, 01 Jan 2024 00:00:00 +0000</pubDate>
      <size>${ADVERTISED_SIZE_BYTES}</size>
      <enclosure url="${TEST_CONFIG.mocks.indexerUrl}/dl/${release.file}" length="${ADVERTISED_SIZE_BYTES}" type="application/x-bittorrent" />
      <torznab:attr name="category" value="${release.category}" />
      <torznab:attr name="seeders" value="20" />
      <torznab:attr name="peers" value="25" />
    </item>`,
    )
    .join('');

  return `<?xml version="1.0" encoding="UTF-8"?>
<rss version="2.0" xmlns:atom="http://www.w3.org/2005/Atom" xmlns:torznab="http://torznab.com/schemas/2015/feed">
  <channel>
    <title>E2E Indexer</title>
    <link>${TEST_CONFIG.mocks.indexerUrl}/</link>
    <description>E2E Indexer</description>${items}
  </channel>
</rss>`;
}

/** Answers one search mode with the given releases. An empty list means no results. */
export function torznabSearchStub(mode: TorznabSearchMode, releases: TorznabRelease[]): Mapping {
  return {
    priority: 5,
    request: {
      method: 'GET',
      urlPath: '/api',
      queryParameters: { t: { equalTo: mode } },
    },
    response: {
      status: 200,
      headers: { 'Content-Type': 'application/xml' },
      body: feed(releases),
    },
  };
}

/** Serves the torrent a release points at. */
export function torznabTorrentStub(file: string, metainfo: Buffer): Mapping {
  return {
    request: { method: 'GET', urlPath: `/dl/${file}` },
    response: {
      status: 200,
      headers: { 'Content-Type': 'application/x-bittorrent' },
      base64Body: metainfo.toString('base64'),
    },
  };
}

export interface GrabbableRelease {
  /** Release title, which is also the torrent name the arr queue reports. */
  title: string;
  /** Uppercase infohash, matching the arr queue's downloadId. */
  downloadId: string;
  mappings: Mapping[];
}

/**
 * Builds a release the arr can actually grab: a torrent, the search response
 * that offers it, and the download link that serves it.
 *
 * The torrent's name is the release title, so the title the arr reports in its
 * queue — and therefore the title Cleanuparr records in `grabbedItems` — is the
 * one the test asked for.
 */
export function grabbableRelease(
  mode: TorznabSearchMode,
  title: string,
  category: number,
): GrabbableRelease {
  const file = `${title}.torrent`;
  const torrent = buildSingleFileTorrent(TORRENT_SOURCE_DIR, title, 32_768, 'http://127.0.0.1:6969/announce');

  return {
    title,
    downloadId: torrent.infoHash.toUpperCase(),
    mappings: [
      torznabSearchStub(mode, [{ title, category, file }]),
      torznabTorrentStub(file, torrent.metainfo),
    ],
  };
}
