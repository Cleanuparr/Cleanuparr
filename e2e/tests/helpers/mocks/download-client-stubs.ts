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

export function transmissionSessionStub(sessionId = 'test-session'): Mapping {
  return {
    request: { method: 'POST', urlPath: '/transmission/rpc' },
    response: {
      status: 200,
      jsonBody: { result: 'success', arguments: { 'rpc-version': 17 } },
      headers: { 'X-Transmission-Session-Id': sessionId },
    },
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

export function utorrentTokenStub(token = 'utorrent-token'): Mapping {
  return {
    request: { method: 'GET', urlPath: '/gui/token.html' },
    response: {
      status: 200,
      body: `<html><div id='token' style='display:none;'>${token}</div></html>`,
      headers: { 'Content-Type': 'text/html', 'Set-Cookie': `GUID=test-guid; Path=/` },
    },
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

export async function applyQBitDefaults(dlc: WireMockClient): Promise<void> {
  await dlc.stubMany([qbitVersionStub(), qbitLoginOkStub(), qbitTorrentsStub()]);
}
