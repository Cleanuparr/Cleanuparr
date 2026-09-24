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

