export type TorrentClientType = 'qBittorrent' | 'Transmission' | 'Deluge' | 'uTorrent' | 'rTorrent';

/**
 * Minimal driver surface used by the orphaned-files spec. Each implementation
 * wraps a specific torrent client's HTTP API and exposes:
 *
 *   - `ready()` — block until the client is accepting requests
 *   - `addTorrent({ metainfo, savePath, name })` — register a torrent whose
 *     data already exists on disk (no actual downloading)
 *   - `deleteTorrent(hash, { deleteFiles })` — remove a torrent from the
 *     client; the spec always passes deleteFiles=false to leave the orphan
 *     on disk so the cleaner has something to detect
 *   - `listTorrents()` — used to assert state after operations
 *
 * `host` is the URL the *Cleanuparr backend* should be configured with — not
 * necessarily the URL the test helper itself talks to (some clients require
 * a different sub-path for their RPC endpoint).
 */
export interface TorrentClientDriver {
  readonly typeName: TorrentClientType;
  /** Hostname+path the Cleanuparr backend uses to reach this client. */
  readonly cleanuparrHost: string;
  readonly username?: string;
  readonly password?: string;

  ready(): Promise<void>;
  addTorrent(input: { metainfo: Buffer; savePath: string; name: string; infoHash: string }): Promise<void>;
  deleteTorrent(infoHash: string): Promise<void>;
  listTorrents(): Promise<Array<{ hash: string; name: string }>>;
  /**
   * Remove every torrent currently registered with the client without deleting
   * data on disk. Called at the start of each test to make the spec
   * idempotent across re-runs (the torrent client's state persists in its
   * config volume between `make test` invocations).
   */
  clearAllTorrents(): Promise<void>;
}

export class ClientNotImplementedError extends Error {
  constructor(client: TorrentClientType, detail: string) {
    super(`${client}: ${detail}`);
    this.name = 'ClientNotImplementedError';
  }
}

export async function pollUntilOk(
  fn: () => Promise<boolean>,
  { timeoutMs = 90_000, intervalMs = 1500, label = 'condition' }: { timeoutMs?: number; intervalMs?: number; label?: string } = {},
): Promise<void> {
  const start = Date.now();
  let lastError: unknown;
  while (Date.now() - start < timeoutMs) {
    try {
      if (await fn()) return;
    } catch (err) {
      lastError = err;
    }
    await new Promise((r) => setTimeout(r, intervalMs));
  }
  throw new Error(`Timed out waiting for ${label} after ${timeoutMs}ms (last error: ${String(lastError)})`);
}
