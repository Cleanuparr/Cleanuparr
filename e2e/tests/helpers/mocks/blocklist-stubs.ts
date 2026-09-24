import type { Mapping } from './wiremock-client';

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

