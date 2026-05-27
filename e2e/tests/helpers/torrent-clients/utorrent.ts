import { TorrentClientDriver, pollUntilOk } from './types';

/**
 * µTorrent driver (WebUI HTTP API).
 *
 * The legacy uTorrent Server for Linux is reanimated by the `ekho/utorrent`
 * Docker image. Auth is HTTP Basic; the WebUI also requires a CSRF token
 * fetched from /gui/token.html plus a `GUID` cookie set by that same call.
 *
 * The list endpoint returns a JSON object whose `torrents` field is an array
 * of arrays — each row is `[hash, status, name, size, ...]`.
 */
export class UTorrentDriver implements TorrentClientDriver {
  readonly typeName = 'uTorrent' as const;
  readonly cleanuparrHost: string;
  readonly username: string;
  readonly password: string;
  private readonly directHost: string;
  private token = '';
  private cookie = '';

  constructor(host = 'http://localhost:8083', username = 'admin', password = '') {
    this.cleanuparrHost = host;
    this.directHost = host;
    this.username = username;
    this.password = password;
  }

  private authHeader(): string {
    return 'Basic ' + Buffer.from(`${this.username}:${this.password}`).toString('base64');
  }

  private requestHeaders(): Record<string, string> {
    const h: Record<string, string> = { Authorization: this.authHeader() };
    if (this.cookie) h.Cookie = this.cookie;
    return h;
  }

  async ready(): Promise<void> {
    await pollUntilOk(
      async () => {
        try {
          await this.refreshToken();
          return this.token !== '';
        } catch {
          return false;
        }
      },
      { label: 'uTorrent WebUI' },
    );
  }

  private async refreshToken(): Promise<void> {
    const res = await fetch(`${this.directHost}/gui/token.html`, {
      headers: { Authorization: this.authHeader() },
    });
    if (!res.ok) {
      throw new Error(`uTorrent token: ${res.status}`);
    }
    const text = await res.text();
    const match = text.match(/<div[^>]*id=['"]token['"][^>]*>([^<]+)<\/div>/);
    if (!match) {
      throw new Error(`uTorrent token not found in response body: ${text.slice(0, 200)}`);
    }
    this.token = match[1];
    const setCookie = res.headers.get('set-cookie');
    if (setCookie) {
      this.cookie = setCookie.split(';')[0];
    }
  }

  async addTorrent({ metainfo, name }: { metainfo: Buffer; savePath: string; name: string; infoHash: string }): Promise<void> {
    const form = new FormData();
    form.append('torrent_file', new Blob([new Uint8Array(metainfo)]), `${name}.torrent`);
    const url = `${this.directHost}/gui/?token=${encodeURIComponent(this.token)}&action=add-file`;
    const res = await fetch(url, {
      method: 'POST',
      headers: this.requestHeaders(),
      body: form,
    });
    if (!res.ok) {
      throw new Error(`uTorrent add: ${res.status} ${await res.text()}`);
    }
  }

  async deleteTorrent(infoHash: string): Promise<void> {
    // `remove` removes the torrent from the client without touching files;
    // `removedata` / `removedatatorrent` delete data and torrent file.
    const url = `${this.directHost}/gui/?token=${encodeURIComponent(this.token)}&action=remove&hash=${infoHash.toUpperCase()}`;
    const res = await fetch(url, { headers: this.requestHeaders() });
    if (!res.ok) {
      throw new Error(`uTorrent remove: ${res.status}`);
    }
  }

  async listTorrents(): Promise<Array<{ hash: string; name: string }>> {
    const url = `${this.directHost}/gui/?token=${encodeURIComponent(this.token)}&list=1`;
    const res = await fetch(url, { headers: this.requestHeaders() });
    if (!res.ok) {
      throw new Error(`uTorrent list: ${res.status}`);
    }
    const body: { torrents?: unknown[][] } = await res.json();
    return (body.torrents ?? []).map((row) => ({
      hash: String(row[0]).toLowerCase(),
      name: String(row[2]),
    }));
  }

  async clearAllTorrents(): Promise<void> {
    const all = await this.listTorrents();
    for (const t of all) {
      const url = `${this.directHost}/gui/?token=${encodeURIComponent(this.token)}&action=remove&hash=${t.hash.toUpperCase()}`;
      await fetch(url, { headers: this.requestHeaders() });
    }
  }
}
