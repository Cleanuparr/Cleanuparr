import type { Mapping, WireMockClient } from './wiremock-client';

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

export function qbitLoginFailStub(): Mapping {
  return {
    request: { method: 'POST', urlPath: '/api/v2/auth/login' },
    response: { status: 200, body: 'Fails.', headers: { 'Content-Type': 'text/plain' } },
    priority: 10,
  };
}

export function qbitTorrentsStub(torrents: Array<Record<string, unknown>> = []): Mapping {
  return {
    request: { method: 'GET', urlPath: '/api/v2/torrents/info' },
    response: { status: 200, jsonBody: torrents },
  };
}

/**
 * Transmission's real wire protocol: first POST to /transmission/rpc without an
 * X-Transmission-Session-Id header returns 409 + a fresh session id; the
 * client must replay the request with that header. We model both phases with
 * two stubs distinguished by request-header presence.
 */
export function transmissionSessionStub(sessionId = 'test-session'): Mapping[] {
  return [
    // Phase 1: client has no session id yet → reject with 409 + session header.
    {
      request: {
        method: 'POST',
        urlPath: '/transmission/rpc',
        headers: { 'X-Transmission-Session-Id': { absent: true } },
      },
      response: {
        status: 409,
        body: '<html><title>409: Conflict</title><body>Conflict</body></html>',
        headers: {
          'X-Transmission-Session-Id': sessionId,
          'Content-Type': 'text/html; charset=ISO-8859-1',
        },
      },
      priority: 1,
    },
    // Phase 2: client retries with the session header → success.
    {
      request: {
        method: 'POST',
        urlPath: '/transmission/rpc',
        headers: { 'X-Transmission-Session-Id': { equalTo: sessionId } },
      },
      response: {
        status: 200,
        jsonBody: {
          result: 'success',
          arguments: {
            version: '4.0.6',
            'rpc-version': 17,
            'rpc-version-minimum': 1,
          },
        },
        headers: {
          'Content-Type': 'application/json',
          'X-Transmission-Session-Id': sessionId,
        },
      },
      priority: 5,
    },
  ];
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

/**
 * @deprecated Prefer {@link utorrentStubs} which also registers the list stub.
 * Kept for backwards-compatible imports.
 */
export function utorrentTokenStub(token = 'utorrent-token'): Mapping {
  return utorrentStubs(token)[0];
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

export async function applyQBitDefaults(dlc: WireMockClient): Promise<void> {
  await dlc.stubMany([qbitVersionStub(), qbitLoginOkStub(), qbitTorrentsStub()]);
}
