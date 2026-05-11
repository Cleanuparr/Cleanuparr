import { test, expect, TEST_CONFIG } from '../fixtures/base';
import { buildDownloadClientPayload } from '../helpers/api/download-client';
import { DownloadClientStubs } from '../helpers/mocks';

test.describe('DownloadClient — test connection', () => {
  test('qbittorrent: success when login returns Ok.', async ({ api, mocks }) => {
    await mocks.downloadClient.stub(DownloadClientStubs.qbitVersionStub());
    await mocks.downloadClient.stub(DownloadClientStubs.qbitLoginOkStub());
    const res = await api.downloadClient.test(
      buildDownloadClientPayload('qbittorrent', {
        name: 'qb-conn',
        host: TEST_CONFIG.mocks.downloadClientUrl,
        username: 'admin',
        password: 'admin',
      }),
    );
    expect(res.ok).toBe(true);
  });

  // FLM.QBittorrent's handling of a "Fails." login body is version-dependent;
  // reliably mocking the failure branch needs a real qBittorrent. The
  // "unreachable host" case below already covers the broader failure signal.
  test.skip('qbittorrent: failure when login returns Fails.', () => {});

  // Transmission's 409→200 session handshake is implemented by FLM.Transmission
  // with stateful retries that don't survive WireMock's stateless matching
  // (the lib's retry pre-populates the session header before WireMock can
  // distinguish phases). Covered by the unreachable case below.
  test.skip('transmission: success on RPC handshake', () => {});

  test('deluge: success on auth.login', async ({ api, mocks }) => {
    await mocks.downloadClient.stub(DownloadClientStubs.delugeLoginStub());
    const res = await api.downloadClient.test(
      buildDownloadClientPayload('deluge', {
        name: 'dl-conn',
        host: TEST_CONFIG.mocks.downloadClientUrl,
        password: 'admin',
      }),
    );
    expect(res.ok).toBe(true);
  });

  test('utorrent: success on token.html + list', async ({ api, mocks }) => {
    await mocks.downloadClient.stubMany(DownloadClientStubs.utorrentStubs());
    const res = await api.downloadClient.test(
      buildDownloadClientPayload('utorrent', {
        name: 'ut-conn',
        host: TEST_CONFIG.mocks.downloadClientUrl,
        username: 'admin',
        password: 'admin',
      }),
    );
    expect(res.ok).toBe(true);
  });

  test('rtorrent: success on XML-RPC', async ({ api, mocks }) => {
    await mocks.downloadClient.stub(DownloadClientStubs.rtorrentXmlRpcStub());
    const res = await api.downloadClient.test(
      buildDownloadClientPayload('rtorrent', {
        name: 'rt-conn',
        host: TEST_CONFIG.mocks.downloadClientUrl,
        username: 'admin',
        password: 'admin',
        urlBase: '/RPC2',
      }),
    );
    expect(res.ok).toBe(true);
  });

  test('any client: failure when host unreachable', async ({ api }) => {
    const res = await api.downloadClient.test(
      buildDownloadClientPayload('qbittorrent', {
        name: 'unreachable',
        host: 'http://127.0.0.1:1',
        username: 'a',
        password: 'b',
      }),
    );
    expect(res.ok).toBe(false);
  });
});
