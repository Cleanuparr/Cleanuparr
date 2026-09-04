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

/** Sentinel a write op stores, lowercase because Deluge only accepts such label ids. */
const PROBE_VALUE = 'probecase';

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
 * What a by-hash operation did.
 *   found: the client resolved the hash, and for a write the new value landed
 *   empty: the call succeeded but resolved nothing, as uTorrent and Deluge do
 *   error: the call failed or the client raised a fault, as rTorrent does
 */
type ProbeOutcome = 'found' | 'empty' | 'error';

interface ProbeResult {
  outcome: ProbeOutcome;
  /** Evidence for the outcome, printed in the matrix. */
  detail: string;
}

/** One operation the production code performs, probed with a hash of a chosen casing. */
interface ProbeOp {
  /** Call shape as the backend issues it, printed in the matrix. */
  readonly name: string;
  /**
   * Runs the operation against `hash`.
   *
   * A write op writes through `hash` and then reads the value back through
   * `nativeHash`, because rTorrent and uTorrent both answer an unusable hash with
   * a success-shaped response, so only the read-back shows whether anything
   * happened. Every write restores the value it replaced.
   */
  run(hash: string, nativeHash: string): Promise<ProbeResult>;
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
  /** The production operations this client is probed with. */
  readonly ops: ProbeOp[];
  /** Performs the same handshake as the matching driver. */
  connect(): Promise<void>;
  /** Info hashes exactly as the client spells them. */
  nativeHashes(): Promise<string[]>;
}

class RawQBittorrent implements RawClient {
  private readonly host = 'http://localhost:8090';
  private cookie = '';

  readonly ops: ProbeOp[] = [
    { name: 'GET torrents/info?hashes=', run: (hash) => this.probeInfo(hash) },
    { name: 'GET torrents/properties?hash=', run: (hash) => this.probeProperties(hash) },
    { name: 'GET torrents/files?hash=', run: (hash) => this.probeFiles(hash) },
  ];

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

  private async get(path: string): Promise<unknown> {
    const res = await fetch(`${this.host}/api/v2/${path}`, { headers: this.headers() });
    if (!res.ok) {
      throw new Error(`qBittorrent ${path}: ${res.status} ${(await res.text()).trim().slice(0, 80)}`);
    }
    return res.json();
  }

  private async info(query: string): Promise<Array<{ hash: string }>> {
    return (await this.get(`torrents/info${query}`)) as Array<{ hash: string }>;
  }

  async nativeHashes(): Promise<string[]> {
    return (await this.info('')).map((t) => t.hash);
  }

  private async probeInfo(hash: string): Promise<ProbeResult> {
    const items = await this.info(`?hashes=${hash}`);
    if (items.length === 0) {
      return { outcome: 'empty', detail: '[] returned' };
    }
    return { outcome: 'found', detail: `hash=${items[0].hash}` };
  }

  private async probeProperties(hash: string): Promise<ProbeResult> {
    const props = (await this.get(`torrents/properties?hash=${hash}`)) as Record<string, unknown> | null;
    if (!props || Object.keys(props).length === 0) {
      return { outcome: 'empty', detail: '{} returned' };
    }
    return { outcome: 'found', detail: `save_path=${String(props.save_path)}` };
  }

  private async probeFiles(hash: string): Promise<ProbeResult> {
    const files = (await this.get(`torrents/files?hash=${hash}`)) as Array<{ name: string }>;
    if (files.length === 0) {
      return { outcome: 'empty', detail: '[] returned' };
    }
    return { outcome: 'found', detail: `${files.length} file(s)` };
  }
}

class RawTransmission implements RawClient {
  private readonly rpc = 'http://localhost:9091/transmission/rpc';
  private readonly auth = `Basic ${Buffer.from('transmission:transmission').toString('base64')}`;
  private sessionId = '';

  // Every Transmission write path resolves a numeric torrent id, never a hash, so
  // there is nothing beyond the lookup to probe.
  readonly ops: ProbeOp[] = [{ name: 'torrent-get ids:[hash]', run: (hash) => this.probeGet(hash) }];

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

  private async probeGet(hash: string): Promise<ProbeResult> {
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
  private readonly json = 'http://localhost:8112/json';
  private cookie = '';
  private requestId = 1;

  readonly ops: ProbeOp[] = [
    { name: 'web.get_torrent_status', run: (hash) => this.probeStatus(hash) },
    { name: 'web.get_torrent_files', run: (hash) => this.probeFiles(hash) },
    { name: 'label.set_torrent', run: (hash, native) => this.probeSetLabel(hash, native) },
  ];

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

  private async probeStatus(hash: string): Promise<ProbeResult> {
    const result = (await this.post('web.get_torrent_status', [hash, ['name']])) as Record<string, unknown> | null;
    const fields = Object.keys(result ?? {});
    if (fields.length === 0) {
      return { outcome: 'empty', detail: '{} returned' };
    }
    return { outcome: 'found', detail: `name=${String((result as Record<string, unknown>).name)}` };
  }

  private async probeFiles(hash: string): Promise<ProbeResult> {
    const result = (await this.post('web.get_torrent_files', [hash])) as Record<string, unknown> | null;
    const fields = Object.keys(result ?? {});
    if (fields.length === 0) {
      return { outcome: 'empty', detail: result === null ? 'null returned' : '{} returned' };
    }
    return { outcome: 'found', detail: `keys=${fields.join(',')}` };
  }

  /** `web.get_torrent_status` answers `label` with '' whatever the plugin holds, so the read goes to the daemon. */
  private async label(hash: string): Promise<string> {
    const result = (await this.post('core.get_torrents_status', [{}, ['label']])) as Record<
      string,
      { label?: string }
    > | null;
    return result?.[hash]?.label ?? '';
  }

  private async probeSetLabel(hash: string, nativeHash: string): Promise<ProbeResult> {
    // set_torrent rejects a label that was never added.
    const labels = (await this.post('label.get_labels', [])) as string[];
    if (!labels.includes(PROBE_VALUE)) {
      await this.post('label.add', [PROBE_VALUE]);
    }

    const before = await this.label(nativeHash);
    await this.post('label.set_torrent', [hash, PROBE_VALUE]);
    const after = await this.label(nativeHash);
    if (after !== PROBE_VALUE) {
      return { outcome: 'empty', detail: `label stayed ${after === '' ? '(none)' : after}` };
    }

    await this.post('label.set_torrent', [nativeHash, before]);
    return { outcome: 'found', detail: `label became ${PROBE_VALUE}` };
  }
}

class RawUTorrent implements RawClient {
  private readonly host = 'http://localhost:8083';
  private readonly auth = `Basic ${Buffer.from('admin:').toString('base64')}`;
  private token = '';
  private cookie = '';

  readonly ops: ProbeOp[] = [
    { name: 'action=getfiles&hash=', run: (hash) => this.probeFiles(hash) },
    { name: 'action=getprops&hash=', run: (hash) => this.probeProps(hash) },
    { name: 'action=setprops&hash=&s=label', run: (hash, native) => this.probeSetLabel(hash, native) },
  ];

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
    const body = (await res.json()) as Record<string, unknown>;
    if (typeof body.error === 'string') {
      throw new Error(`uTorrent ${query}: ${body.error}`);
    }
    return body;
  }

  async nativeHashes(): Promise<string[]> {
    const body = (await this.get('list=1')) as { torrents?: unknown[][] };
    return (body.torrents ?? []).map((row) => String(row[0]));
  }

  private async probeFiles(hash: string): Promise<ProbeResult> {
    const body = await this.get(`action=getfiles&hash=${hash}`);
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

  private async props(hash: string): Promise<Record<string, unknown> | undefined> {
    const body = (await this.get(`action=getprops&hash=${hash}`)) as { props?: Array<Record<string, unknown>> };
    return body.props?.[0];
  }

  private async probeProps(hash: string): Promise<ProbeResult> {
    const props = await this.props(hash);
    if (!props) {
      return { outcome: 'empty', detail: 'props:[] returned' };
    }
    return { outcome: 'found', detail: `echoed hash ${String(props.hash)}` };
  }

  /** getprops carries no label, so the read goes to the list row, where it sits at index 11. */
  private async label(hash: string): Promise<string> {
    const body = (await this.get('list=1')) as { torrents?: unknown[][] };
    const row = (body.torrents ?? []).find((r) => String(r[0]) === hash);
    return row ? String(row[11]) : '';
  }

  private async probeSetLabel(hash: string, nativeHash: string): Promise<ProbeResult> {
    const before = await this.label(nativeHash);
    await this.get(`action=setprops&hash=${hash}&s=label&v=${PROBE_VALUE}`);
    const after = await this.label(nativeHash);
    if (after !== PROBE_VALUE) {
      return { outcome: 'empty', detail: `label stayed ${after === '' ? '(none)' : after}` };
    }

    await this.get(`action=setprops&hash=${nativeHash}&s=label&v=${before}`);
    return { outcome: 'found', detail: `label became ${PROBE_VALUE}` };
  }
}

class RawRTorrent implements RawClient {
  private readonly rpc = 'http://localhost:8088/RPC2';

  readonly ops: ProbeOp[] = [
    { name: 'd.name', run: (hash) => this.probeName(hash) },
    { name: 'd.is_private', run: (hash) => this.probeIsPrivate(hash) },
    { name: 'f.multicall f.path=', run: (hash) => this.probeMulticall('f.multicall', hash, 'f.path=') },
    { name: 't.multicall t.url=', run: (hash) => this.probeMulticall('t.multicall', hash, 't.url=') },
    { name: 'd.custom1', run: (hash, native) => this.probeCustom1Read(hash, native) },
    { name: 'd.custom1.set', run: (hash, native) => this.probeCustom1Write(hash, native) },
  ];

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

  /** First scalar of a response, whichever XML-RPC type rTorrent wrapped it in. */
  private static scalar(xml: string): string | undefined {
    return xml.match(/<(string|i4|i8|boolean)>([\s\S]*?)<\/\1>/)?.[2];
  }

  private async custom1(hash: string): Promise<string | undefined> {
    return RawRTorrent.scalar(await this.call('d.custom1', [hash]));
  }

  async nativeHashes(): Promise<string[]> {
    // Every string in a d.hash-only multicall response is an info hash.
    const xml = await this.call('d.multicall2', ['', 'main', 'd.hash=']);
    return [...xml.matchAll(/<string>([0-9a-fA-F]{40})<\/string>/g)].map((m) => m[1]);
  }

  private async probeName(hash: string): Promise<ProbeResult> {
    const xml = await this.call('d.name', [hash]);
    const name = RawRTorrent.scalar(xml) ?? '';
    if (name === '') {
      return { outcome: 'empty', detail: 'empty d.name' };
    }
    return { outcome: 'found', detail: `d.name=${name}` };
  }

  private async probeIsPrivate(hash: string): Promise<ProbeResult> {
    const value = RawRTorrent.scalar(await this.call('d.is_private', [hash]));
    if (value === undefined) {
      return { outcome: 'empty', detail: 'no scalar in response' };
    }
    return { outcome: 'found', detail: `d.is_private=${value}` };
  }

  private async probeMulticall(method: string, hash: string, field: string): Promise<ProbeResult> {
    const xml = await this.call(method, [hash, '', field]);
    const count = [...xml.matchAll(/<string>/g)].length;
    if (count === 0) {
      return { outcome: 'empty', detail: `no ${field} rows` };
    }
    return { outcome: 'found', detail: `${count} ${field} row(s)` };
  }

  private async probeCustom1Read(hash: string, nativeHash: string): Promise<ProbeResult> {
    const before = (await this.custom1(nativeHash)) ?? '';
    // Seeding separates a resolving read from an unknown hash answering blank.
    await this.call('d.custom1.set', [nativeHash, PROBE_VALUE]);
    try {
      const value = (await this.custom1(hash)) ?? '';
      if (value !== PROBE_VALUE) {
        return { outcome: 'empty', detail: `d.custom1=${value === '' ? '(none)' : value}` };
      }
      return { outcome: 'found', detail: `d.custom1=${value}` };
    } finally {
      await this.call('d.custom1.set', [nativeHash, before]);
    }
  }

  private async probeCustom1Write(hash: string, nativeHash: string): Promise<ProbeResult> {
    const before = (await this.custom1(nativeHash)) ?? '';
    await this.call('d.custom1.set', [hash, PROBE_VALUE]);
    const after = (await this.custom1(nativeHash)) ?? '';
    if (after !== PROBE_VALUE) {
      return { outcome: 'empty', detail: `d.custom1 stayed ${after === '' ? '(none)' : after}` };
    }

    await this.call('d.custom1.set', [nativeHash, before]);
    return { outcome: 'found', detail: `d.custom1 became ${PROBE_VALUE}` };
  }
}

interface ClientUnderProbe {
  key: string;
  /** Used for setup and teardown only, never for the measurement itself. */
  driver: DriverLike;
  /** Host directory bind-mounted as the client's /downloads. */
  slug: string;
  raw: RawClient;
  /** What we believe a flipped-case hash does, printed next to what it did. */
  expectation: 'resolves' | 'does not resolve';
  /** Carried into every row of this client, for facts a probe cannot measure. */
  clientNote?: string;
}

const clients: ClientUnderProbe[] = [
  { key: 'qBittorrent', driver: new QBittorrentDriver(), slug: 'qbittorrent', raw: new RawQBittorrent(), expectation: 'resolves' },
  {
    key: 'Transmission',
    driver: new TransmissionDriver(),
    slug: 'transmission',
    raw: new RawTransmission(),
    expectation: 'resolves',
    clientNote: 'writes take a numeric Id, not a hash, so only the lookup is probed',
  },
  { key: 'Deluge', driver: new DelugeDriver(), slug: 'deluge', raw: new RawDeluge(), expectation: 'does not resolve' },
  { key: 'uTorrent', driver: new UTorrentDriver(), slug: 'utorrent', raw: new RawUTorrent(), expectation: 'resolves' },
  { key: 'rTorrent', driver: new RTorrentDriver(), slug: 'rtorrent', raw: new RawRTorrent(), expectation: 'does not resolve' },
];

interface ProbeRow {
  client: string;
  reachable: boolean;
  nativeCasing: string;
  nativeHash: string;
  op: string;
  flippedHash: string;
  /** The native-cased call, proving the raw transport works at all. */
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

function addNote(row: ProbeRow, text: string): void {
  row.note = row.note ? `${row.note}; ${text}` : text;
}

/** Runs one raw operation, turning a rejected call into the `error` outcome. */
async function runOp(op: ProbeOp, hash: string, nativeHash: string): Promise<ProbeResult> {
  try {
    return await op.run(hash, nativeHash);
  } catch (err) {
    return { outcome: 'error', detail: errorText(err) };
  }
}

/** A flipped result only counts once the native-cased control resolved. */
function isConclusive(row: ProbeRow): boolean {
  return row.control === 'found' && row.flipped !== 'n/a';
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
  const header = ['client', 'native casing', 'op', 'native call', 'flipped call', 'expected', 'matches'];
  const body = rows.map((r) => [
    r.client,
    r.reachable ? r.nativeCasing : 'unreachable',
    r.op,
    r.control,
    r.flipped,
    r.expectation,
    isConclusive(r) ? String((r.flipped === 'found') === (r.expectation === 'resolves')) : '?',
  ]);
  const widths = header.map((h, i) => Math.max(h.length, ...body.map((row) => row[i].length)));
  const line = (cells: string[]): string => cells.map((c, i) => c.padEnd(widths[i])).join('  ').trimEnd();

  const table = [line(header), line(widths.map((w) => '-'.repeat(w))), ...body.map(line)];
  const detail = rows.flatMap((r) => [
    `${r.client} ${r.op}:`,
    `  native hash   ${r.nativeHash || '(none read)'}`,
    `  flipped hash  ${r.flippedHash || '(not probed)'}`,
    `  native call   ${r.control}: ${r.controlDetail || '-'}`,
    `  flipped call  ${r.flipped}: ${r.flippedDetail || '-'}`,
    ...(r.note ? [`  note          ${r.note}`] : []),
  ]);

  const label = (r: ProbeRow): string => `${r.client} ${r.op} (${r.flipped})`;
  const conclusive = rows.filter(isConclusive);
  const sensitive = conclusive.filter((r) => r.flipped !== 'found').map(label);
  const insensitive = conclusive.filter((r) => r.flipped === 'found').map(label);
  const inconclusive = rows.filter((r) => !isConclusive(r)).map((r) => `${r.client} ${r.op}`);

  const summary = [
    `case-SENSITIVE: ${sensitive.length > 0 ? sensitive.join(', ') : 'none'}`,
    `case-insensitive: ${insensitive.length > 0 ? insensitive.join(', ') : 'none'}`,
    ...(inconclusive.length > 0 ? [`inconclusive: ${inconclusive.join(', ')}`] : []),
  ];

  return [...table, '', ...detail, '', ...summary].join('\n');
}

/**
 * Measures whether each download client resolves a torrent by an info hash whose
 * case differs from the one it reports natively, across the operations the backend
 * actually performs. Every outcome is recorded rather than asserted: the point is
 * to publish what the clients actually do, including results that contradict what
 * we assumed.
 */
test.describe('Download client hash casing', () => {
  test('records which by-hash operations are case-sensitive per client', async () => {
    test.setTimeout(600_000);
    mkdirShared(HOST_DOWNLOADS);

    const rows: ProbeRow[] = [];

    for (const c of clients) {
      const clientRows: ProbeRow[] = c.raw.ops.map((op) => ({
        client: c.key,
        reachable: false,
        nativeCasing: '-',
        nativeHash: '',
        op: op.name,
        flippedHash: '',
        control: 'n/a',
        controlDetail: '',
        flipped: 'n/a',
        flippedDetail: '',
        expectation: c.expectation,
        note: c.clientNote ?? '',
      }));
      rows.push(...clientRows);

      try {
        await c.driver.ready();
        await c.driver.clearAllTorrents();
        await c.raw.connect();
        for (const row of clientRows) {
          row.reachable = true;
        }

        // One torrent serves every op, so each write op puts back what it replaced.
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
          for (const row of clientRows) {
            addNote(row, `raw list never reported ${fixture.infoHash}`);
          }
          continue;
        }
        const flippedHash = flipCase(native);

        for (const [index, op] of c.raw.ops.entries()) {
          const row = clientRows[index];
          row.nativeHash = native;
          row.nativeCasing = classifyCasing(native);
          row.flippedHash = flippedHash;

          const control = await runOp(op, native, native);
          row.control = control.outcome;
          row.controlDetail = control.detail;

          const flipped = await runOp(op, flippedHash, native);
          row.flipped = flipped.outcome;
          row.flippedDetail = flipped.detail;

          if (control.outcome !== 'found') {
            addNote(row, 'the native-cased call did not resolve either, so the flipped result proves nothing');
          }
        }
      } catch (err) {
        for (const row of clientRows) {
          addNote(row, errorText(err));
        }
      } finally {
        await c.driver.clearAllTorrents().catch(() => {});
      }
    }

    const matrix = renderMatrix(rows);
    console.log(`\n${matrix}\n`);
    await test.info().attach('hash-casing-matrix.txt', { body: matrix, contentType: 'text/plain' });

    const unreachable = [...new Set(rows.filter((r) => !r.reachable).map((r) => r.client))];
    expect.soft(unreachable, 'clients the probe could not reach').toEqual([]);
  });
});
