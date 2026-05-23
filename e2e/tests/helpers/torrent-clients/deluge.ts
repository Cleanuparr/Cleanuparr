import { TorrentClientDriver, pollUntilOk } from './types';

/**
 * Deluge driver (Web UI JSON-RPC at /json).
 *
 * Auth flow:
 *   - POST { method: 'auth.login', params: [password] } — sets session cookie
 *   - POST { method: 'web.connected' } — true once Web UI is connected to a daemon
 *   - POST { method: 'web.connect', params: [host_id] } — pick the first
 *     daemon if Web UI isn't connected yet
 *
 * Default linuxserver/deluge web password is `deluge`.
 */
export class DelugeDriver implements TorrentClientDriver {
  readonly typeName = 'Deluge' as const;
  readonly cleanuparrHost: string;
  readonly username = '';
  readonly password: string;
  private readonly directJson: string;
  private cookie: string | null = null;
  private requestId = 1;

  constructor(host = 'http://localhost:8112', password = 'deluge') {
    this.cleanuparrHost = host;
    this.password = password;
    this.directJson = `${host.replace(/\/$/, '')}/json`;
  }

  async ready(): Promise<void> {
    await pollUntilOk(
      async () => {
        const res = await fetch(this.directJson, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ method: 'auth.login', params: [this.password], id: this.requestId++ }),
        });
        if (!res.ok) return false;
        const setCookie = res.headers.get('set-cookie');
        if (setCookie) this.cookie = setCookie.split(';')[0];
        const body = await res.json();
        return body.result === true;
      },
      { label: 'Deluge Web UI' },
    );

    // Ensure Web UI is bound to the local daemon. On a fresh install the
    // Web UI starts unconnected and `core.*` calls fail until we connect.
    const connected = await this.call<boolean>('web.connected', []);
    if (!connected) {
      const hosts = await this.call<Array<Array<string>>>('web.get_hosts', []);
      const firstHost = hosts?.[0]?.[0];
      if (firstHost) {
        await this.call('web.connect', [firstHost]);
      }
    }
  }

  private async call<T>(method: string, params: unknown[]): Promise<T> {
    const res = await fetch(this.directJson, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...(this.cookie ? { Cookie: this.cookie } : {}),
      },
      body: JSON.stringify({ method, params, id: this.requestId++ }),
    });
    if (!res.ok) {
      throw new Error(`Deluge ${method} failed: ${res.status} ${await res.text()}`);
    }
    const body = await res.json();
    if (body.error) {
      throw new Error(`Deluge ${method} error: ${JSON.stringify(body.error)}`);
    }
    return body.result as T;
  }

  async addTorrent({ metainfo, savePath, name }: { metainfo: Buffer; savePath: string; name: string; infoHash: string }): Promise<void> {
    const filename = `${name}.torrent`;
    const b64 = metainfo.toString('base64');
    await this.call('core.add_torrent_file', [
      filename,
      b64,
      {
        download_location: savePath,
        add_paused: true,
        seed_mode: true, // skip hash check — treat as already complete
      },
    ]);
  }

  async deleteTorrent(infoHash: string): Promise<void> {
    // remove_torrent signature: (torrent_id, remove_data: bool)
    await this.call('core.remove_torrent', [infoHash, false]);
  }

  async clearAllTorrents(): Promise<void> {
    const all = await this.listTorrents();
    for (const t of all) {
      await this.call('core.remove_torrent', [t.hash, false]);
    }
  }

  async listTorrents(): Promise<Array<{ hash: string; name: string }>> {
    const result = await this.call<Record<string, { name: string }>>('core.get_torrents_status', [{}, ['name']]);
    return Object.entries(result ?? {}).map(([hash, info]) => ({ hash, name: info.name }));
  }
}
