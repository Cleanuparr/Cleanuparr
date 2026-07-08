import type { Mapping, WireMockClient } from './wiremock-client';

const DEFAULT_BLOCKLIST = [
  '*.exe',
  '*.bat',
  '*.cmd',
  '*.scr',
  '*.com',
  '*.iso',
  '*.zipx',
  '*.lnk',
].join('\n');

export function blocklistResponseStub(content: string = DEFAULT_BLOCKLIST, urlPath = '/blacklist'): Mapping {
  return {
    request: { method: 'GET', urlPath },
    response: { status: 200, body: content, headers: { 'Content-Type': 'text/plain' } },
  };
}

export async function applyBlocklistDefaults(blocklist: WireMockClient): Promise<void> {
  await blocklist.stubMany([blocklistResponseStub()]);
}
