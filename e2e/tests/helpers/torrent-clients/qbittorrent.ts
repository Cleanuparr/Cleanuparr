import { TorrentClientDriver, pollUntilOk } from './types';

/**
 * qBittorrent driver (WebUI v2).
 *
 * Auth note: relies on the linuxserver/qbittorrent default of bypassing auth
 * for localhost. Combined with `network_mode: host`, requests from the test
 * runner originate from 127.0.0.1, so login is skipped. If running against
 * a qBittorrent without localhost-bypass, set `username` and `password` and
 * the driver will POST /api/v2/auth/login.
 */
export class QBittorrentDriver implements TorrentClientDriver {
  readonly typeName = 'qBittorrent' as const;
  readonly cleanuparrHost: string;
  readonly username?: string;
  readonly password?: string;
  private readonly directHost: string;
  private cookie: string | null = null;

  constructor(host = 'http://localhost:8090', username = 'admin', password = 'adminadmin') {
    this.cleanuparrHost = host;
    this.directHost = host;
    this.username = username;
    this.password = password;
  }

  async ready(): Promise<void> {
    await pollUntilOk(
      async () => {
        const res = await fetch(`${this.directHost}/api/v2/app/version`, {
          headers: this.cookie ? { Cookie: this.cookie } : undefined,
        });
        return res.ok || res.status === 403;
      },
      { label: 'qBittorrent WebUI' },
    );
    if (this.username && this.password) {
      await this.login();
    }
  }

  private async login(): Promise<void> {
    const body = new URLSearchParams({ username: this.username!, password: this.password! });
    const res = await fetch(`${this.directHost}/api/v2/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: body.toString(),
    });
    if (!res.ok) {
      throw new Error(`qBittorrent login failed: ${res.status} ${await res.text()}`);
    }
    const cookie = res.headers.get('set-cookie');
    if (cookie) {
      // Strip flags — Node's fetch returns the full header
      this.cookie = cookie.split(';')[0];
    }
  }

  async addTorrent({ metainfo, savePath }: { metainfo: Buffer; savePath: string; name: string; infoHash: string }): Promise<void> {
    const form = new FormData();
    form.append('torrents', new Blob([new Uint8Array(metainfo)]), 'torrent.torrent');
    form.append('savepath', savePath);
    form.append('paused', 'true');
    form.append('skip_checking', 'true');
    form.append('autoTMM', 'false');
    const res = await fetch(`${this.directHost}/api/v2/torrents/add`, {
      method: 'POST',
      headers: this.cookie ? { Cookie: this.cookie } : undefined,
      body: form,
    });
    if (!res.ok) {
      throw new Error(`qBittorrent add failed: ${res.status} ${await res.text()}`);
    }
  }

  async deleteTorrent(infoHash: string): Promise<void> {
    const body = new URLSearchParams({ hashes: infoHash, deleteFiles: 'false' });
    const res = await fetch(`${this.directHost}/api/v2/torrents/delete`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/x-www-form-urlencoded',
        ...(this.cookie ? { Cookie: this.cookie } : {}),
      },
      body: body.toString(),
    });
    if (!res.ok) {
      throw new Error(`qBittorrent delete failed: ${res.status} ${await res.text()}`);
    }
  }

  async clearAllTorrents(): Promise<void> {
    const all = await this.listTorrents();
    if (all.length === 0) return;
    const body = new URLSearchParams({
      hashes: all.map((t) => t.hash).join('|'),
      deleteFiles: 'false',
    });
    const res = await fetch(`${this.directHost}/api/v2/torrents/delete`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/x-www-form-urlencoded',
        ...(this.cookie ? { Cookie: this.cookie } : {}),
      },
      body: body.toString(),
    });
    if (!res.ok) {
      throw new Error(`qBittorrent clear failed: ${res.status}`);
    }
  }

  async listTorrents(): Promise<Array<{ hash: string; name: string }>> {
    const res = await fetch(`${this.directHost}/api/v2/torrents/info`, {
      headers: this.cookie ? { Cookie: this.cookie } : undefined,
    });
    if (!res.ok) {
      throw new Error(`qBittorrent list failed: ${res.status}`);
    }
    const items: Array<{ hash: string; name: string }> = await res.json();
    return items.map((t) => ({ hash: t.hash, name: t.name }));
  }
}
