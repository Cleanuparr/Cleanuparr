import type { Mapping } from './wiremock-client';

/**
 * Convenience stub bundles for qBittorrent / Transmission / Deluge / uTorrent / rTorrent.
 * The stubs simulate authentication handshakes and torrent listings.
 */

export function qbitVersionStub(version = '5.0.0'): Mapping {
  return {
    request: { method: 'GET', urlPath: '/api/v2/app/version' },
    response: { status: 200, body: version, headers: { 'Content-Type': 'text/plain' } },
  };
}

export function qbitLoginOkStub(sid = 'test-sid'): Mapping {
  return {
    request: { method: 'POST', urlPath: '/api/v2/auth/login' },
    response: {
      status: 200,
      body: 'Ok.',
      headers: { 'Content-Type': 'text/plain', 'Set-Cookie': `SID=${sid}; Path=/` },
    },
  };
}

export function qbitTorrentsStub(torrents: Array<Record<string, unknown>> = []): Mapping {
  return {
    request: { method: 'GET', urlPath: '/api/v2/torrents/info' },
    response: { status: 200, jsonBody: torrents },
  };
}

export function delugeLoginStub(sessionCookie = 'test-deluge'): Mapping {
  return {
    request: {
      method: 'POST',
      urlPath: '/json',
      bodyPatterns: [{ matchesJsonPath: '$.method' }],
    },
    response: {
      status: 200,
      jsonBody: { id: 1, result: true, error: null },
      headers: { 'Set-Cookie': `_session_id=${sessionCookie}; Path=/` },
    },
  };
}

/**
 * µTorrent flow:
 *  1. GET /gui/token.html → HTML body containing `<div id="token">…</div>` plus
 *     a `Set-Cookie: GUID=…` header.
 *  2. GET /gui/?list=1&token=… → JSON `{"torrents":[]}` to satisfy
 *     {@link UTorrentResponseParser.ParseTorrentList}.
 */
export function utorrentStubs(token = 'utorrent-token', guid = 'test-guid'): Mapping[] {
  return [
    {
      request: { method: 'GET', urlPath: '/gui/token.html' },
      response: {
        status: 200,
        body: `<html><div id='token' style='display:none;'>${token}</div></html>`,
        headers: {
          'Content-Type': 'text/html',
          'Set-Cookie': `GUID=${guid}; Path=/`,
        },
      },
    },
    {
      request: { method: 'GET', urlPath: '/gui/' },
      response: {
        status: 200,
        jsonBody: { torrents: [], torrentc: '0', label: [] },
        headers: { 'Content-Type': 'application/json' },
      },
    },
  ];
}

export interface SabQueueSlotStub {
  nzo_id: string;
  filename?: string;
  status?: string;
  cat?: string;
}

export function sabQueueStub(slots: SabQueueSlotStub[] = [], kbpersec = '0.00', paused = false): Mapping {
  return {
    request: { method: 'GET', urlPath: '/api', queryParameters: { mode: { equalTo: 'queue' } } },
    response: {
      status: 200,
      jsonBody: {
        queue: {
          paused,
          kbpersec,
          slots: slots.map((s) => ({ filename: s.nzo_id, status: 'Downloading', cat: '*', ...s })),
        },
      },
    },
  };
}

export interface SabHistorySlotStub {
  nzo_id: string;
  name?: string;
  status?: string;
  storage?: string;
  fail_message?: string;
}

export function sabHistoryStub(slots: SabHistorySlotStub[] = []): Mapping {
  return {
    request: { method: 'GET', urlPath: '/api', queryParameters: { mode: { equalTo: 'history' } } },
    response: {
      status: 200,
      jsonBody: {
        history: {
          slots: slots.map((s) => ({ name: s.nzo_id, status: 'Completed', storage: '', fail_message: '', ...s })),
        },
      },
    },
  };
}

export function sabUnreachableStub(): Mapping {
  return {
    request: { method: 'GET', urlPath: '/api' },
    response: { status: 500 },
  };
}

/** A 200 response with no `queue`/`history` key at all: version drift or a proxy rewrite. */
export function sabEmptyBodyStub(mode: 'queue' | 'history'): Mapping {
  return {
    request: { method: 'GET', urlPath: '/api', queryParameters: { mode: { equalTo: mode } } },
    response: { status: 200, jsonBody: {} },
  };
}

export function sabGetConfigStub(downloadDir: string): Mapping {
  return {
    request: { method: 'GET', urlPath: '/api', queryParameters: { mode: { equalTo: 'get_config' } } },
    response: { status: 200, jsonBody: { config: { misc: { download_dir: downloadDir } } } },
  };
}

export function sabGetConfigErrorStub(): Mapping {
  return {
    request: { method: 'GET', urlPath: '/api', queryParameters: { mode: { equalTo: 'get_config' } } },
    response: { status: 500 },
  };
}

/** Real SABnzbd rejects `mode=queue` (and any other authenticated mode) for a wrong apikey with HTTP 403 plain text. */
export function sabRejectedApiKeyStub(apiKey: string): Mapping {
  return {
    request: { method: 'GET', urlPath: '/api', queryParameters: { mode: { equalTo: 'queue' }, apikey: { equalTo: apiKey } } },
    response: { status: 403, body: 'API Key Incorrect' },
    priority: 1,
  };
}

export function rtorrentXmlRpcStub(): Mapping {
  return {
    request: { method: 'POST', urlPath: '/RPC2' },
    response: {
      status: 200,
      body: '<?xml version="1.0"?><methodResponse><params><param><value><string>0.9.8</string></value></param></params></methodResponse>',
      headers: { 'Content-Type': 'text/xml' },
    },
  };
}

