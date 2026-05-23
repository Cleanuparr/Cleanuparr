import { TorrentClientDriver, pollUntilOk } from './types';

/**
 * Transmission driver (transmission-rpc protocol).
 *
 * Transmission requires a CSRF-style session id obtained by issuing any RPC
 * call and reading the `X-Transmission-Session-Id` header from the 409
 * response, then replaying with that header. We refresh the id transparently
 * on each request.
 *
 * Compose wires linuxserver/transmission with USER=transmission /
 * PASS=transmission, which gates the RPC endpoint behind basic auth.
 */
export class TransmissionDriver implements TorrentClientDriver {
  readonly typeName = 'Transmission' as const;
  readonly cleanuparrHost: string;
  readonly username: string;
  readonly password: string;
  private readonly directRpc: string;
  private sessionId = '';

  constructor(host = 'http://localhost:9091/transmission', username = 'transmission', password = 'transmission') {
    this.cleanuparrHost = host;
    this.username = username;
    this.password = password;
    this.directRpc = `${host.replace(/\/$/, '')}/rpc`;
  }

  async ready(): Promise<void> {
    await pollUntilOk(
      async () => {
        try {
          await this.call('session-get', {});
          return true;
        } catch {
          return false;
        }
      },
      { label: 'Transmission RPC' },
    );
  }

  private authHeader(): string {
    return 'Basic ' + Buffer.from(`${this.username}:${this.password}`).toString('base64');
  }

  private async call(method: string, args: Record<string, unknown>): Promise<any> {
    const send = async () => {
      return fetch(this.directRpc, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': this.authHeader(),
          'X-Transmission-Session-Id': this.sessionId,
        },
        body: JSON.stringify({ method, arguments: args }),
      });
    };
    let res = await send();
    if (res.status === 409) {
      this.sessionId = res.headers.get('x-transmission-session-id') ?? '';
      res = await send();
    }
    if (!res.ok) {
      throw new Error(`Transmission ${method} failed: ${res.status} ${await res.text()}`);
    }
    const body = await res.json();
    if (body.result !== 'success') {
      throw new Error(`Transmission ${method} non-success: ${body.result}`);
    }
    return body.arguments;
  }

  async addTorrent({ metainfo, savePath, infoHash }: { metainfo: Buffer; savePath: string; name: string; infoHash: string }): Promise<void> {
    await this.call('torrent-add', {
      metainfo: metainfo.toString('base64'),
      'download-dir': savePath,
      paused: true,
    });
    // The torrent is added in a paused, unverified state. Transmission will
    // try to verify on resume — we never resume, so it stays in stopped/
    // queued state with savePath populated, which is enough for the cleaner
    // to pick it up via GetAllTorrentsLite.
    void infoHash;
  }

  async deleteTorrent(infoHash: string): Promise<void> {
    await this.call('torrent-remove', {
      ids: [infoHash],
      'delete-local-data': false,
    });
  }

  async clearAllTorrents(): Promise<void> {
    const all = await this.listTorrents();
    if (all.length === 0) return;
    await this.call('torrent-remove', {
      ids: all.map((t) => t.hash),
      'delete-local-data': false,
    });
  }

  async listTorrents(): Promise<Array<{ hash: string; name: string }>> {
    const args = await this.call('torrent-get', { fields: ['hashString', 'name'] });
    return (args.torrents ?? []).map((t: { hashString: string; name: string }) => ({ hash: t.hashString, name: t.name }));
  }
}
