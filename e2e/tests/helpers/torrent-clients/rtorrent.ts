import { TorrentClientDriver, pollUntilOk } from './types';

/**
 * rTorrent driver via XML-RPC over HTTP. linuxserver/rutorrent exposes
 * SCGI-backed XML-RPC at `/RPC2` on the rutorrent web port (8088 by default).
 *
 * rTorrent is the most awkward of the supported clients to drive from a
 * test runner because:
 *   - It uses XML-RPC (not JSON), so we hand-build the request/response
 *   - There is no native "skip hash check" — we use `load.raw` (load only,
 *     no auto-start) so rTorrent never tries to peer or verify pieces.
 *
 * If the spec for this client fails because of XML escaping or because the
 * rutorrent nginx isn't routing /RPC2, this file is the most likely place
 * to need adjustment.
 */
export class RTorrentDriver implements TorrentClientDriver {
  readonly typeName = 'rTorrent' as const;
  readonly cleanuparrHost: string;
  readonly username?: string;
  readonly password?: string;
  private readonly directRpc: string;

  constructor(host = 'http://localhost:8088/RPC2') {
    this.cleanuparrHost = host;
    this.directRpc = host;
  }

  async ready(): Promise<void> {
    await pollUntilOk(
      async () => {
        try {
          await this.call('system.client_version', []);
          return true;
        } catch {
          return false;
        }
      },
      { label: 'rTorrent XML-RPC' },
    );
  }

  private async call(method: string, params: Array<XmlRpcValue>): Promise<any> {
    const xml = buildXmlRpcRequest(method, params);
    const res = await fetch(this.directRpc, {
      method: 'POST',
      headers: { 'Content-Type': 'text/xml' },
      body: xml,
    });
    if (!res.ok) {
      throw new Error(`rTorrent ${method} failed: ${res.status} ${await res.text()}`);
    }
    const text = await res.text();
    return parseXmlRpcResponse(text);
  }

  async addTorrent({ metainfo, savePath, name, infoHash }: { metainfo: Buffer; savePath: string; name: string; infoHash: string }): Promise<void> {
    // load.raw_start_verbose loads AND starts the torrent. Starting triggers
    // an immediate hash check, which (for our tiny 32KB files matching the
    // metainfo piece hashes) populates `d.base_path` — the field Cleanuparr
    // reads as the torrent's save path. Without starting, `d.base_path`
    // stays empty and Cleanuparr can't build a claimed-paths set.
    await this.call('load.raw_start_verbose', [
      '',
      { type: 'base64', value: metainfo.toString('base64') },
      `d.directory.set="${savePath}"`,
      `d.custom1.set="${name}"`,
    ]);
    void infoHash;
  }

  async deleteTorrent(infoHash: string): Promise<void> {
    // d.erase removes the torrent from rTorrent's session without touching
    // the data on disk.
    await this.call('d.erase', [infoHash.toUpperCase()]);
  }

  async clearAllTorrents(): Promise<void> {
    const all = await this.listTorrents();
    for (const t of all) {
      try {
        await this.call('d.erase', [t.hash.toUpperCase()]);
      } catch {
        // best-effort: continue clearing
      }
    }
  }

  async listTorrents(): Promise<Array<{ hash: string; name: string }>> {
    const result = await this.call('d.multicall2', ['', 'main', 'd.hash=', 'd.name=']);
    if (!Array.isArray(result)) return [];
    return result.map((row: unknown) => {
      const arr = row as unknown[];
      return { hash: String(arr[0]).toLowerCase(), name: String(arr[1]) };
    });
  }
}

type XmlRpcValue = string | number | boolean | { type: 'base64'; value: string };

function buildXmlRpcRequest(method: string, params: XmlRpcValue[]): string {
  const paramsXml = params.map((p) => `<param>${encodeValue(p)}</param>`).join('');
  return `<?xml version="1.0"?><methodCall><methodName>${escapeXml(method)}</methodName><params>${paramsXml}</params></methodCall>`;
}

function encodeValue(v: XmlRpcValue): string {
  if (typeof v === 'number') {
    return Number.isInteger(v) ? `<value><int>${v}</int></value>` : `<value><double>${v}</double></value>`;
  }
  if (typeof v === 'boolean') {
    return `<value><boolean>${v ? 1 : 0}</boolean></value>`;
  }
  if (typeof v === 'string') {
    return `<value><string>${escapeXml(v)}</string></value>`;
  }
  if (v && typeof v === 'object' && v.type === 'base64') {
    return `<value><base64>${v.value}</base64></value>`;
  }
  throw new Error(`xml-rpc: unsupported value ${typeof v}`);
}

function escapeXml(s: string): string {
  return s.replace(/[<>&'"]/g, (c) => ({ '<': '&lt;', '>': '&gt;', '&': '&amp;', "'": '&apos;', '"': '&quot;' }[c]!));
}

function parseXmlRpcResponse(xml: string): unknown {
  if (xml.includes('<fault>')) {
    const msg = xml.match(/<name>faultString<\/name>\s*<value><string>([^<]+)<\/string>/)?.[1] ?? xml;
    throw new Error(`xml-rpc fault: ${msg}`);
  }
  const paramsMatch = xml.match(/<params>([\s\S]*?)<\/params>/);
  if (!paramsMatch) return null;
  return parseValue(paramsMatch[1]);
}

function parseValue(xml: string): unknown {
  // Very small subset of XML-RPC parsing: handles int/string/boolean/array/struct/base64.
  const tag = xml.match(/<value>\s*<([a-zA-Z0-9]+)>/);
  if (!tag) {
    // Bare <value>text</value> is treated as string per spec.
    const bare = xml.match(/<value>([\s\S]*?)<\/value>/);
    return bare ? bare[1].trim() : null;
  }
  const type = tag[1];
  if (type === 'array') {
    // Greedy — for nested arrays, we want the OUTER </data> not the first inner one.
    const inner = xml.match(/<array>\s*<data>([\s\S]*)<\/data>\s*<\/array>/)?.[1] ?? '';
    return splitValues(inner).map(parseValue);
  }
  if (type === 'struct') {
    const inner = xml.match(/<struct>([\s\S]*)<\/struct>/)?.[1] ?? '';
    const out: Record<string, unknown> = {};
    const memberRe = /<member>\s*<name>([^<]+)<\/name>\s*([\s\S]*?)<\/member>/g;
    let m: RegExpExecArray | null;
    while ((m = memberRe.exec(inner)) !== null) {
      out[m[1]] = parseValue(m[2]);
    }
    return out;
  }
  const scalar = xml.match(new RegExp(`<${type}>([\\s\\S]*?)<\\/${type}>`))?.[1] ?? '';
  if (type === 'int' || type === 'i4') return Number(scalar);
  if (type === 'boolean') return scalar === '1';
  if (type === 'double') return Number(scalar);
  return decodeXml(scalar);
}

function splitValues(xml: string): string[] {
  const out: string[] = [];
  let depth = 0;
  let start = -1;
  for (let i = 0; i < xml.length; i++) {
    if (xml.startsWith('<value>', i)) {
      if (depth === 0) start = i;
      depth++;
      i += '<value>'.length - 1;
    } else if (xml.startsWith('</value>', i)) {
      depth--;
      if (depth === 0 && start !== -1) {
        out.push(xml.slice(start, i + '</value>'.length));
        start = -1;
      }
      i += '</value>'.length - 1;
    }
  }
  return out;
}

function decodeXml(s: string): string {
  return s.replace(/&(lt|gt|amp|apos|quot);/g, (_, e) => ({ lt: '<', gt: '>', amp: '&', apos: "'", quot: '"' }[e as 'lt']!));
}
