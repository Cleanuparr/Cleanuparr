import { TEST_CONFIG } from './test-config';
import { WireMockClient } from './mocks/wiremock-client';

/**
 * Direct access to the real Sonarr and Radarr containers.
 *
 * The live-arr specs drive Cleanuparr through its own API like every other
 * spec. This helper is for the other side of the contract: reading what the arr
 * itself ended up with, and cleaning it up between tests.
 */

export interface LiveArrQueueRecord {
  id: number;
  title: string;
  downloadId: string;
  status: string;
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
  ) {}

  private async request(path: string, init?: RequestInit): Promise<unknown> {
    const res = await fetch(`${this.url}${path}`, {
      ...init,
      headers: { 'X-Api-Key': this.apiKey, 'Content-Type': 'application/json', ...(init?.headers ?? {}) },
    });

    if (!res.ok) {
      throw new Error(`${init?.method ?? 'GET'} ${path} on ${this.url} returned ${res.status}: ${await res.text()}`);
    }

    const text = await res.text();
    return text ? JSON.parse(text) : null;
  }

  async queue(): Promise<LiveArrQueueRecord[]> {
    const body = (await this.request('/api/v3/queue?pageSize=200')) as { records?: LiveArrQueueRecord[] };
    return body.records ?? [];
  }

  async commands(): Promise<LiveArrCommand[]> {
    return (await this.request('/api/v3/command')) as LiveArrCommand[];
  }

  /**
   * The arr only moves a grabbed download into its queue on its own refresh
   * cycle, which runs once a minute. Tests trigger the same command so they do
   * not have to wait for it.
   */
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

export const liveSonarr = new LiveArr(TEST_CONFIG.liveArr.sonarrUrl, TEST_CONFIG.liveArr.sonarrApiKey);
export const liveRadarr = new LiveArr(TEST_CONFIG.liveArr.radarrUrl, TEST_CONFIG.liveArr.radarrApiKey);

/**
 * The fake Torznab indexer. It is not part of {@link MockServers} because the
 * other suites run without its container.
 */
export const indexerMock = new WireMockClient(TEST_CONFIG.mocks.indexerAdminUrl);
