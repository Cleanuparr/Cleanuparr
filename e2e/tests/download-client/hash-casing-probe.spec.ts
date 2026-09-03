import { test, expect } from '@playwright/test';
import { join, resolve } from 'node:path';
import { QBittorrentDriver } from '../helpers/torrent-clients/qbittorrent';
import { TransmissionDriver } from '../helpers/torrent-clients/transmission';
import { DelugeDriver } from '../helpers/torrent-clients/deluge';
import { UTorrentDriver } from '../helpers/torrent-clients/utorrent';
import { RTorrentDriver } from '../helpers/torrent-clients/rtorrent';
import { buildFolderTorrent } from '../helpers/torrent-fixtures';
import { mkdirShared } from '../helpers/shared-volume';

const HOST_DOWNLOADS = resolve(__dirname, '..', '..', 'test-data', 'downloads');
const CLIENT_DOWNLOADS = '/downloads';

function sleep(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

/** Driver surface this probe uses, all of it setup or teardown. */
interface DriverLike {
  readonly typeName: string;
  ready(): Promise<void>;
  clearAllTorrents(): Promise<void>;
  addTorrent(input: { metainfo: Buffer; savePath: string; name: string; infoHash: string }): Promise<void>;
  listTorrents(): Promise<Array<{ hash: string; name: string }>>;
}

/**
 * What a by-hash read did.
 *   found: the client resolved the hash to the torrent
 *   empty: the call succeeded but resolved nothing, as uTorrent and Deluge do
 *   error: the call failed or the client raised a fault, as rTorrent does
 */
type ProbeOutcome = 'found' | 'empty' | 'error';

interface ProbeResult {
  outcome: ProbeOutcome;
  /** Evidence for the outcome, printed in the matrix. */
  detail: string;
}

/**
 * A transport that talks to one client without the driver in the way.
 *
 * The drivers normalise casing before they send a hash and after they read one
 * (`rtorrent.ts` upper-cases every `d.*` argument, `qbittorrent.ts` lower-cases,
 * `utorrent.ts` upper-cases on write and lower-cases on read), so a probe issued
 * through a driver would measure the driver. Every implementation here passes the
 * hash through verbatim and reports what the client returns verbatim.
 */
interface RawClient {
  /** Human-readable name of the operation `probe` issues. */
  readonly probeOp: string;
  /** Performs the same handshake as the matching driver. */
  connect(): Promise<void>;
  /** Info hashes exactly as the client spells them. */
  nativeHashes(): Promise<string[]>;
  /** Reads one torrent by hash. Throws when the client rejects the call. */
  probe(hash: string): Promise<ProbeResult>;
}

class RawQBittorrent implements RawClient {
  readonly probeOp = 'GET torrents/info?hashes=';
  private readonly host = 'http://localhost:8090';
  private cookie = '';

  async connect(): Promise<void> {
    const body = new URLSearchParams({ username: 'admin', password: 'adminadmin' });
    const res = await fetch(`${this.host}/api/v2/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: body.toString(),
    });
    const setCookie = res.headers.get('set-cookie');
    if (setCookie) {
      this.cookie = setCookie.split(';')[0];
    }
    // A host-network qBittorrent whitelists 127.0.0.1, so a missing cookie is not fatal.
  }

  private headers(): Record<string, string> {
    return this.cookie ? { Cookie: this.cookie } : {};
  }

  private async info(query: string): Promise<Array<{ hash: string }>> {
    const res = await fetch(`${this.host}/api/v2/torrents/info${query}`, { headers: this.headers() });
    if (!res.ok) {
      throw new Error(`qBittorrent info: ${res.status} ${await res.text()}`);
    }
    return (await res.json()) as Array<{ hash: string }>;
  }

  async nativeHashes(): Promise<string[]> {
    return (await this.info('')).map((t) => t.hash);
  }

  async probe(hash: string): Promise<ProbeResult> {
    const items = await this.info(`?hashes=${hash}`);
    if (items.length === 0) {
      return { outcome: 'empty', detail: '[] returned' };
    }
    return { outcome: 'found', detail: `hash=${items[0].hash}` };
  }
}

class RawTransmission implements RawClient {
  readonly probeOp = 'torrent-get ids:[hash]';
  private readonly rpc = 'http://localhost:9091/transmission/rpc';
  private readonly auth = `Basic ${Buffer.from('transmission:transmission').toString('base64')}`;
  private sessionId = '';

  private async post(
    method: string,
    args: Record<string, unknown>,
  ): Promise<{ result: string; torrents: Array<{ hashString: string }> }> {
    const send = () =>
      fetch(this.rpc, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: this.auth,
          'X-Transmission-Session-Id': this.sessionId,
        },
        body: JSON.stringify({ method, arguments: args }),
      });

    let res = await send();
    if (res.status === 409) {
      this.sessionId = res.headers.get('x-transmission-session-id') ?? '';
      res = await send();
    }
    if (!res.ok) {
      throw new Error(`Transmission ${method}: ${res.status} ${await res.text()}`);
    }
    const body = (await res.json()) as { result?: string; arguments?: { torrents?: Array<{ hashString: string }> } };
    return { result: body.result ?? '', torrents: body.arguments?.torrents ?? [] };
  }

  async connect(): Promise<void> {
    await this.post('session-get', {});
  }

  async nativeHashes(): Promise<string[]> {
    const { result, torrents } = await this.post('torrent-get', { fields: ['hashString'] });
    if (result !== 'success') {
      throw new Error(`Transmission torrent-get: ${result}`);
    }
    return torrents.map((t) => t.hashString);
  }

  async probe(hash: string): Promise<ProbeResult> {
    const { result, torrents } = await this.post('torrent-get', { ids: [hash], fields: ['hashString'] });
    if (result !== 'success') {
      return { outcome: 'error', detail: `result=${result}` };
    }
    if (torrents.length === 0) {
      return { outcome: 'empty', detail: 'torrents:[] returned' };
    }
    return { outcome: 'found', detail: `hashString=${torrents[0].hashString}` };
  }
}

class RawDeluge implements RawClient {
  readonly probeOp = 'web.get_torrent_status';
  private readonly json = 'http://localhost:8112/json';
  private cookie = '';
  private requestId = 1;

  private async post(method: string, params: unknown[]): Promise<unknown> {
    const res = await fetch(this.json, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...(this.cookie ? { Cookie: this.cookie } : {}),
      },
      body: JSON.stringify({ method, params, id: this.requestId++ }),
    });
    if (!res.ok) {
      throw new Error(`Deluge ${method}: ${res.status} ${await res.text()}`);
    }
    const setCookie = res.headers.get('set-cookie');
    if (setCookie) {
      this.cookie = setCookie.split(';')[0];
    }
    const body = (await res.json()) as { result?: unknown; error?: unknown };
    if (body.error) {
      throw new Error(`Deluge ${method}: ${JSON.stringify(body.error)}`);
    }
    return body.result;
  }

  async connect(): Promise<void> {
    // The Web UI's link to the daemon is server-side state that driver.ready() establishes.
    if ((await this.post('auth.login', ['deluge'])) !== true) {
      throw new Error('Deluge auth.login rejected the password');
    }
  }

  async nativeHashes(): Promise<string[]> {
    const result = (await this.post('core.get_torrents_status', [{}, ['name']])) as Record<string, unknown> | null;
    return Object.keys(result ?? {});
  }

  async probe(hash: string): Promise<ProbeResult> {
    const result = (await this.post('web.get_torrent_status', [hash, ['name']])) as Record<string, unknown> | null;
    const fields = Object.keys(result ?? {});
    if (fields.length === 0) {
      return { outcome: 'empty', detail: '{} returned' };
    }
    return { outcome: 'found', detail: `name=${String((result as Record<string, unknown>).name)}` };
  }
}

class RawUTorrent implements RawClient {
  readonly probeOp = 'action=getfiles&hash=';
  private readonly host = 'http://localhost:8083';
  private readonly auth = `Basic ${Buffer.from('admin:').toString('base64')}`;
  private token = '';
  private cookie = '';

  async connect(): Promise<void> {
    const res = await fetch(`${this.host}/gui/token.html`, { headers: { Authorization: this.auth } });
    if (!res.ok) {
      throw new Error(`uTorrent token: ${res.status}`);
    }
    const text = await res.text();
    const match = text.match(/<div[^>]*id=['"]token['"][^>]*>([^<]+)<\/div>/);
    if (!match) {
      throw new Error(`uTorrent token missing from ${text.slice(0, 120)}`);
    }
    this.token = match[1];
    const setCookie = res.headers.get('set-cookie');
    if (setCookie) {
      this.cookie = setCookie.split(';')[0];
    }
  }

  private async get(query: string): Promise<Record<string, unknown>> {
    const headers: Record<string, string> = { Authorization: this.auth };
    if (this.cookie) {
      headers.Cookie = this.cookie;
    }
    const res = await fetch(`${this.host}/gui/?token=${encodeURIComponent(this.token)}&${query}`, { headers });
    if (!res.ok) {
      throw new Error(`uTorrent ${query}: ${res.status}`);
    }
    return (await res.json()) as Record<string, unknown>;
  }

  async nativeHashes(): Promise<string[]> {
    const body = (await this.get('list=1')) as { torrents?: unknown[][] };
    return (body.torrents ?? []).map((row) => String(row[0]));
  }

  async probe(hash: string): Promise<ProbeResult> {
    const body = await this.get(`action=getfiles&hash=${hash}`);
    if (typeof body.error === 'string') {
      throw new Error(`uTorrent getfiles: ${body.error}`);
    }
    // A found torrent answers files:[hash, [[name, size, downloaded, priority], ...]].
    const files = body.files;
    if (!Array.isArray(files) || files.length < 2) {
      return { outcome: 'empty', detail: `files=${JSON.stringify(files ?? null)}` };
    }
    const entries = files[1];
    const count = Array.isArray(entries) ? entries.length : 0;
    if (count === 0) {
      return { outcome: 'empty', detail: `echoed hash ${String(files[0])} with no files` };
    }
    return { outcome: 'found', detail: `echoed hash ${String(files[0])}, ${count} file(s)` };
  }
}

class RawRTorrent implements RawClient {
  readonly probeOp = 'd.name';
  private readonly rpc = 'http://localhost:8088/RPC2';

  async connect(): Promise<void> {
    await this.call('system.client_version', []);
  }

  /** A hex info hash needs no XML escaping, so the envelope is built inline. */
  private async call(method: string, params: string[]): Promise<string> {
    const paramsXml = params.map((p) => `<param><value><string>${p}</string></value></param>`).join('');
    const res = await fetch(this.rpc, {
      method: 'POST',
      headers: { 'Content-Type': 'text/xml' },
      body: `<?xml version="1.0"?><methodCall><methodName>${method}</methodName><params>${paramsXml}</params></methodCall>`,
    });
    if (!res.ok) {
      throw new Error(`rTorrent ${method}: ${res.status} ${await res.text()}`);
    }
    const xml = await res.text();
    if (xml.includes('<fault>')) {
      const fault = xml.match(/<name>faultString<\/name>\s*<value><string>([^<]*)<\/string>/)?.[1] ?? 'unknown fault';
      throw new Error(`rTorrent ${method} fault: ${fault}`);
    }
    return xml;
  }

  async nativeHashes(): Promise<string[]> {
    // Every string in a d.hash-only multicall response is an info hash.
    const xml = await this.call('d.multicall2', ['', 'main', 'd.hash=']);
    return [...xml.matchAll(/<string>([0-9a-fA-F]{40})<\/string>/g)].map((m) => m[1]);
  }

  async probe(hash: string): Promise<ProbeResult> {
    const xml = await this.call('d.name', [hash]);
    const name = xml.match(/<string>([\s\S]*?)<\/string>/)?.[1] ?? '';
    if (name === '') {
      return { outcome: 'empty', detail: 'empty d.name' };
    }
    return { outcome: 'found', detail: `d.name=${name}` };
  }
}

interface ClientUnderProbe {
  key: string;
  /** Used for setup and teardown only, never for the measurement itself. */
  driver: DriverLike;
  /** Host directory bind-mounted as the client's /downloads. */
  slug: string;
  raw: RawClient;
  /** What we believe the flipped-case read does, printed next to what it did. */
  expectation: 'resolves' | 'does not resolve';
}

const clients: ClientUnderProbe[] = [
  { key: 'qBittorrent', driver: new QBittorrentDriver(), slug: 'qbittorrent', raw: new RawQBittorrent(), expectation: 'resolves' },
  { key: 'Transmission', driver: new TransmissionDriver(), slug: 'transmission', raw: new RawTransmission(), expectation: 'resolves' },
  { key: 'Deluge', driver: new DelugeDriver(), slug: 'deluge', raw: new RawDeluge(), expectation: 'does not resolve' },
  { key: 'uTorrent', driver: new UTorrentDriver(), slug: 'utorrent', raw: new RawUTorrent(), expectation: 'resolves' },
  { key: 'rTorrent', driver: new RTorrentDriver(), slug: 'rtorrent', raw: new RawRTorrent(), expectation: 'does not resolve' },
];

interface ProbeRow {
  client: string;
  reachable: boolean;
  nativeCasing: string;
  nativeHash: string;
  probeOp: string;
  flippedHash: string;
  /** The native-cased read, proving the raw transport works at all. */
  control: ProbeOutcome | 'n/a';
  controlDetail: string;
  flipped: ProbeOutcome | 'n/a';
  flippedDetail: string;
  expectation: string;
  note: string;
}

function flipCase(s: string): string {
  return [...s].map((c) => (c === c.toLowerCase() ? c.toUpperCase() : c.toLowerCase())).join('');
}

function classifyCasing(s: string): string {
  const letters = s.replace(/[^a-zA-Z]/g, '');
  if (letters.length === 0) {
    return 'no letters';
  }
  if (letters === letters.toLowerCase()) {
    return 'lowercase';
  }
  if (letters === letters.toUpperCase()) {
    return 'uppercase';
  }
  return 'mixed';
}

function errorText(err: unknown): string {
  const text = err instanceof Error ? err.message : String(err);
  return text.replace(/\s+/g, ' ').slice(0, 160);
}

/** Runs one raw read, turning a rejected call into the `error` outcome. */
async function runProbe(raw: RawClient, hash: string): Promise<ProbeResult> {
  try {
    return await raw.probe(hash);
  } catch (err) {
    return { outcome: 'error', detail: errorText(err) };
  }
}

async function waitForRegistered(driver: DriverLike, infoHash: string, timeoutMs = 30_000): Promise<void> {
  const want = infoHash.toLowerCase();
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    if ((await driver.listTorrents()).some((t) => t.hash.toLowerCase() === want)) {
      return;
    }
    await sleep(500);
  }
  throw new Error(`torrent ${infoHash} never registered with ${driver.typeName}`);
}

function renderMatrix(rows: ProbeRow[]): string {
  const header = ['client', 'native casing', 'probe op', 'native read', 'flipped read', 'expected', 'matches'];
  const body = rows.map((r) => [
    r.client,
    r.reachable ? r.nativeCasing : 'unreachable',
    r.probeOp,
    r.control,
    r.flipped,
    r.expectation,
    r.flipped === 'n/a' ? '?' : String((r.flipped === 'found') === (r.expectation === 'resolves')),
  ]);
  const widths = header.map((h, i) => Math.max(h.length, ...body.map((row) => row[i].length)));
  const line = (cells: string[]): string => cells.map((c, i) => c.padEnd(widths[i])).join('  ').trimEnd();

  const table = [line(header), line(widths.map((w) => '-'.repeat(w))), ...body.map(line)];
  const detail = rows.flatMap((r) => [
    `${r.client}:`,
    `  native hash   ${r.nativeHash || '(none read)'}`,
    `  flipped hash  ${r.flippedHash || '(not probed)'}`,
    `  native read   ${r.control}: ${r.controlDetail || '-'}`,
    `  flipped read  ${r.flipped}: ${r.flippedDetail || '-'}`,
    ...(r.note ? [`  note          ${r.note}`] : []),
  ]);

  return [...table, '', ...detail].join('\n');
}

/**
 * Measures whether each download client resolves a torrent by an info hash whose
 * case differs from the one it reports natively. Every outcome is recorded rather
 * than asserted: the point is to publish what the clients actually do, including
 * results that contradict what we assumed.
 */
test.describe('Download client hash casing', () => {
  test('records whether a by-hash read is case-sensitive per client', async () => {
    test.setTimeout(300_000);
    mkdirShared(HOST_DOWNLOADS);

    const rows: ProbeRow[] = [];

    for (const c of clients) {
      const row: ProbeRow = {
        client: c.key,
        reachable: false,
        nativeCasing: '-',
        nativeHash: '',
        probeOp: c.raw.probeOp,
        flippedHash: '',
        control: 'n/a',
        controlDetail: '',
        flipped: 'n/a',
        flippedDetail: '',
        expectation: c.expectation,
        note: '',
      };
      rows.push(row);

      try {
        await c.driver.ready();
        await c.driver.clearAllTorrents();
        await c.raw.connect();
        row.reachable = true;

        const fixture = buildFolderTorrent(join(HOST_DOWNLOADS, c.slug), `probe-${c.slug}`);
        await c.driver.addTorrent({
          metainfo: fixture.metainfo,
          savePath: CLIENT_DOWNLOADS,
          name: fixture.name,
          infoHash: fixture.infoHash,
        });
        await waitForRegistered(c.driver, fixture.infoHash);

        const native = (await c.raw.nativeHashes()).find((h) => h.toLowerCase() === fixture.infoHash);
        if (!native) {
          row.note = `raw list never reported ${fixture.infoHash}`;
          continue;
        }
        row.nativeHash = native;
        row.nativeCasing = classifyCasing(native);
        row.flippedHash = flipCase(native);

        const control = await runProbe(c.raw, native);
        row.control = control.outcome;
        row.controlDetail = control.detail;

        const flipped = await runProbe(c.raw, row.flippedHash);
        row.flipped = flipped.outcome;
        row.flippedDetail = flipped.detail;

        if (control.outcome !== 'found') {
          row.note = 'native-cased read did not resolve either, so the flipped result proves nothing';
        }
      } catch (err) {
        row.note = errorText(err);
      } finally {
        await c.driver.clearAllTorrents().catch(() => {});
      }
    }

    const matrix = renderMatrix(rows);
    console.log(`\n${matrix}\n`);
    await test.info().attach('hash-casing-matrix.txt', { body: matrix, contentType: 'text/plain' });

    const unreachable = rows.filter((r) => !r.reachable).map((r) => r.client);
    expect.soft(unreachable, 'clients the probe could not reach').toEqual([]);
  });
});
