import { createServer, IncomingMessage, Server, ServerResponse } from 'node:http';
import { AddressInfo } from 'node:net';

/**
 * Minimal Sonarr/Radarr stub for e2e tests.
 *
 * The Playwright runner hosts this stub on the test machine. The Cleanuparr
 * app container reaches it via `host.docker.internal` — Docker Desktop adds
 * that name automatically, and Linux CI gets it through the `extra_hosts:
 * host.docker.internal:host-gateway` entry in docker-compose.e2e.yml. The
 * stub only models the three endpoints the MalwareBlocker job touches:
 *
 *   - `GET  /api/v3/system/status` — minimal version payload
 *   - `GET  /api/v3/queue?page=N&pageSize=M` — returns whatever records the
 *     test set via `setQueue(...)`; supports paging only enough to satisfy
 *     the iterator (`totalRecords` + `records` for page 1)
 *   - `DELETE /api/v3/queue/{id}` — records the id so the test can assert
 *     the arr-side removal happened, then returns 200
 *
 * Any other request returns 200 with an empty body to avoid spurious errors
 * if Cleanuparr probes endpoints we haven't modeled.
 */
export interface StubQueueRecord {
  id: number;
  downloadId: string;
  title: string;
  protocol: 'torrent' | 'usenet';
  seriesId: number;
  episodeId: number;
  seasonNumber?: number;
  status?: string;
  trackedDownloadStatus?: string;
  trackedDownloadState?: string;
  sizeLeft?: number;
}

export interface StubDeleteCall {
  id: number;
  removeFromClient: boolean;
  blocklist: boolean;
  changeCategory: boolean;
}

export class ArrStubServer {
  private server: Server | null = null;
  private queue: StubQueueRecord[] = [];
  private deletes: StubDeleteCall[] = [];
  private queueRequestCount = 0;
  private listenPort = 0;

  async start(port = 9100): Promise<void> {
    if (this.server) {
      throw new Error('ArrStubServer already started');
    }

    this.server = createServer((req, res) => this.handle(req, res));

    await new Promise<void>((resolve, reject) => {
      const onError = (err: Error) => reject(err);
      this.server!.once('error', onError);
      // Bind on all interfaces so the cleanuparr container can reach us
      // via `host.docker.internal`. 127.0.0.1 would only accept local
      // connections, which Docker Desktop cannot route to.
      this.server!.listen(port, '0.0.0.0', () => {
        this.server!.off('error', onError);
        const addr = this.server!.address() as AddressInfo;
        this.listenPort = addr.port;
        resolve();
      });
    });
  }

  async stop(): Promise<void> {
    if (!this.server) {
      return;
    }
    await new Promise<void>((resolve, reject) => {
      this.server!.close((err) => (err ? reject(err) : resolve()));
    });
    this.server = null;
  }

  get port(): number {
    return this.listenPort;
  }

  get url(): string {
    return `http://127.0.0.1:${this.listenPort}`;
  }

  /** URL the cleanuparr container should use to reach this stub. */
  get containerUrl(): string {
    return `http://host.docker.internal:${this.listenPort}`;
  }

  setQueue(records: StubQueueRecord[]): void {
    this.queue = records;
  }

  resetCounters(): void {
    this.deletes = [];
    this.queueRequestCount = 0;
  }

  getDeletes(): StubDeleteCall[] {
    return [...this.deletes];
  }

  /**
   * Resolves once the stub has received at least one `GET /api/v3/queue`
   * request since the last {@link resetCounters} — i.e. once a MalwareBlocker
   * iteration has actually run against this stub.
   */
  async waitForQueueRequest(timeoutMs = 15_000): Promise<boolean> {
    const start = Date.now();
    while (Date.now() - start < timeoutMs) {
      if (this.queueRequestCount > 0) {
        return true;
      }
      await new Promise((r) => setTimeout(r, 200));
    }
    return false;
  }

  private handle(req: IncomingMessage, res: ServerResponse): void {
    const url = req.url ?? '';
    const method = req.method ?? 'GET';

    if (method === 'GET' && url.startsWith('/api/v3/system/status')) {
      this.sendJson(res, 200, { version: '4.0.0.0', appName: 'Sonarr' });
      return;
    }

    if (method === 'GET' && url.startsWith('/api/v3/queue')) {
      this.queueRequestCount++;
      this.sendJson(res, 200, {
        page: 1,
        pageSize: this.queue.length || 10,
        totalRecords: this.queue.length,
        records: this.queue.map((r) => ({
          id: r.id,
          downloadId: r.downloadId,
          title: r.title,
          protocol: r.protocol,
          seriesId: r.seriesId,
          episodeId: r.episodeId,
          seasonNumber: r.seasonNumber ?? 1,
          status: r.status ?? 'downloading',
          trackedDownloadStatus: r.trackedDownloadStatus ?? 'ok',
          trackedDownloadState: r.trackedDownloadState ?? 'downloading',
          statusMessages: [],
          sizeLeft: r.sizeLeft ?? 0,
        })),
      });
      return;
    }

    const deleteMatch = method === 'DELETE' && /^\/api\/v3\/queue\/(\d+)(?:\?|$)/.exec(url);
    if (deleteMatch) {
      const id = Number(deleteMatch[1]);
      const query = url.includes('?') ? url.slice(url.indexOf('?') + 1) : '';
      const params = new URLSearchParams(query);
      this.deletes.push({
        id,
        removeFromClient: params.get('removeFromClient') === 'true',
        blocklist: params.get('blocklist') === 'true',
        changeCategory: params.get('changeCategory') === 'true',
      });
      this.queue = this.queue.filter((r) => r.id !== id);
      res.statusCode = 200;
      res.end();
      return;
    }

    res.statusCode = 200;
    res.end();
  }

  private sendJson(res: ServerResponse, status: number, body: unknown): void {
    const payload = JSON.stringify(body);
    res.statusCode = status;
    res.setHeader('Content-Type', 'application/json');
    res.setHeader('Content-Length', Buffer.byteLength(payload).toString());
    res.end(payload);
  }
}
