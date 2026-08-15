import { TEST_CONFIG } from './test-config';
import { WireMockClient } from './mocks/wiremock-client';

/**
 * Direct access to the real Sonarr and Radarr containers.
 *
 * The specs drive Cleanuparr through its own API, like every other spec.
 * This is the other side of the contract: what the arr itself ended up with.
 */

export interface LiveArrQueueRecord {
  id: number;
  title: string;
  downloadId: string;
  status: string;
  /** Bytes still to download. Cleanuparr counts a record as active only above zero. */
  sizeleft: number;
  seriesId?: number;
  seasonNumber?: number;
  movieId?: number;
}

export interface LiveArrCommand {
  id: number;
  name: string;
  status: string;
}

export class LiveArr {
  constructor(
    readonly url: string,
    private readonly apiKey: string,
    private readonly queueQuery: string,
  ) {}

  get<T>(path: string): Promise<T> {
    return this.request(path) as Promise<T>;
  }

  post<T>(path: string, body: unknown): Promise<T> {
    return this.request(path, { method: 'POST', body: JSON.stringify(body) }) as Promise<T>;
  }

  put<T>(path: string, body: unknown): Promise<T> {
    return this.request(path, { method: 'PUT', body: JSON.stringify(body) }) as Promise<T>;
  }

  delete(path: string): Promise<unknown> {
    return this.request(path, { method: 'DELETE' });
  }

  /** Creates the tag if the arr does not have it yet, and returns its id. */
  async ensureTag(label: string): Promise<number> {
    const tags = await this.get<Array<{ id: number; label: string }>>('/api/v3/tag');
    const existing = tags.find((tag) => tag.label === label);

    if (existing) {
      return existing.id;
    }

    return (await this.post<{ id: number }>('/api/v3/tag', { label })).id;
  }

  private async request(path: string, init?: RequestInit): Promise<unknown> {
    const res = await fetch(`${this.url}${path}`, {
      ...init,
      headers: { 'X-Api-Key': this.apiKey, 'Content-Type': 'application/json', ...init?.headers },
    });

    if (!res.ok) {
      throw new Error(`${init?.method ?? 'GET'} ${path} on ${this.url} returned ${res.status}: ${await res.text()}`);
    }

    const text = await res.text();
    return text ? JSON.parse(text) : null;
  }

  /** Mirrors the query Cleanuparr sends, so a spec sees the same records it does. */
  async queue(page = 1): Promise<LiveArrQueueRecord[]> {
    const body = await this.queuePage(page);
    return body.records ?? [];
  }

  async queuePage(page = 1): Promise<{ records?: LiveArrQueueRecord[]; totalRecords: number }> {
    return (await this.request(`/api/v3/queue?page=${page}&pageSize=200&${this.queueQuery}`)) as {
      records?: LiveArrQueueRecord[];
      totalRecords: number;
    };
  }

  async commands(): Promise<LiveArrCommand[]> {
    return (await this.request('/api/v3/command')) as LiveArrCommand[];
  }

  /** The arr only queues a grab on its own refresh, which runs once a minute. */
  async refreshMonitoredDownloads(): Promise<void> {
    await this.request('/api/v3/command', {
      method: 'POST',
      body: JSON.stringify({ name: 'RefreshMonitoredDownloads' }),
    });
  }

  /** Drops every queued download from the arr and from the download client. */
  async clearQueue(): Promise<void> {
    for (const record of await this.queue()) {
      await this.request(
        `/api/v3/queue/${record.id}?removeFromClient=true&blocklist=false&skipRedownload=true`,
        { method: 'DELETE' },
      );
    }
  }

  async waitReady(timeoutMs = 120_000): Promise<void> {
    const start = Date.now();
    while (Date.now() - start < timeoutMs) {
      try {
        const res = await fetch(`${this.url}/ping`);
        if (res.ok) {
          return;
        }
      } catch {
        // not up yet
      }
      await new Promise((r) => setTimeout(r, 1_000));
    }

    throw new Error(`Arr at ${this.url} did not become ready within ${timeoutMs}ms`);
  }
}

export const liveSonarr = new LiveArr(
  TEST_CONFIG.liveArr.sonarrUrl,
  TEST_CONFIG.liveArr.sonarrApiKey,
  'includeUnknownSeriesItems=true&includeSeries=true&includeEpisode=true',
);

export const liveRadarr = new LiveArr(
  TEST_CONFIG.liveArr.radarrUrl,
  TEST_CONFIG.liveArr.radarrApiKey,
  'includeUnknownMovieItems=true&includeMovie=true',
);

/** The fake Torznab indexer, kept out of MockServers: other suites lack its container. */
export const indexerMock = new WireMockClient(TEST_CONFIG.mocks.indexerAdminUrl);
