import { test, expect, TEST_CONFIG } from '../fixtures/base';
import { buildDownloadClientPayload } from '../helpers/api/download-client';
import { DownloadClientStubs } from '../helpers/mocks';
import { QBittorrentDriver } from '../helpers/torrent-clients/qbittorrent';
import { TransmissionDriver } from '../helpers/torrent-clients/transmission';

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

  // Note: qBittorrent's "Fails." login response handling is version-specific
  // in FLM.QBittorrent and Transmission's 409→200 session handshake uses
  // stateful retries that don't model cleanly with WireMock. Both failure
  // branches are exercised by the generic "host unreachable" case below.

  // An API key client skips login and probes an auth-protected endpoint instead.
  test('qbittorrent: an api key alone succeeds', async ({ api, mocks }) => {
    await mocks.downloadClient.stub(DownloadClientStubs.qbitSpeedLimitsModeStub('right-key'));
    const res = await api.downloadClient.test(
      buildDownloadClientPayload('qbittorrent', {
        name: 'qb-key-conn',
        host: TEST_CONFIG.mocks.downloadClientUrl,
        apiKey: 'right-key',
      }),
    );
    expect(res.ok).toBe(true);
  });

  test('qbittorrent: a wrong api key fails test-connection', async ({ api, mocks }) => {
    await mocks.downloadClient.stub(DownloadClientStubs.qbitRejectedApiKeyStub());
    const res = await api.downloadClient.test(
      buildDownloadClientPayload('qbittorrent', {
        name: 'qb-key-conn-wrong',
        host: TEST_CONFIG.mocks.downloadClientUrl,
        apiKey: 'wrong-key',
      }),
    );
    expect(res.ok).toBe(false);
  });

  test('qbittorrent: an api key combined with a password is rejected', async ({ api }) => {
    const res = await api.downloadClient.test(
      buildDownloadClientPayload('qbittorrent', {
        name: 'qb-key-plus-password',
        host: TEST_CONFIG.mocks.downloadClientUrl,
        apiKey: 'right-key',
        password: 'admin',
      }),
    );
    expect(res.status).toBe(400);
  });

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

  test('sabnzbd: success on queue (apikey accepted)', async ({ api, mocks }) => {
    await mocks.downloadClient.stub(DownloadClientStubs.sabQueueStub());
    const res = await api.downloadClient.test(
      buildDownloadClientPayload('sabnzbd', {
        name: 'sab-conn',
        host: TEST_CONFIG.mocks.downloadClientUrl,
        apiKey: 'right-key',
      }),
    );
    expect(res.ok).toBe(true);
  });

  // mode=queue requires a valid apikey, unlike mode=version: a wrong key must fail here.
  test('sabnzbd: a wrong api key fails test-connection', async ({ api, mocks }) => {
    await mocks.downloadClient.stub(DownloadClientStubs.sabRejectedApiKeyStub('wrong-key'));
    const res = await api.downloadClient.test(
      buildDownloadClientPayload('sabnzbd', {
        name: 'sab-conn-wrong-key',
        host: TEST_CONFIG.mocks.downloadClientUrl,
        apiKey: 'wrong-key',
      }),
    );
    expect(res.ok).toBe(false);
  });

  // Reproduces #6: a new api key paired with the masked password placeholder must
  // use the new key, not the stored one WireMock has already been told to reject.
  test('sabnzbd: a new api key with a placeholder password succeeds', async ({ api, mocks }) => {
    const created = await (
      await api.downloadClient.create(
        buildDownloadClientPayload('sabnzbd', {
          name: 'sab-resolve-conn',
          host: TEST_CONFIG.mocks.downloadClientUrl,
          password: 'stored-password',
          apiKey: 'old-key',
        }),
      )
    ).json();

    await mocks.downloadClient.stub(DownloadClientStubs.sabRejectedApiKeyStub('old-key'));
    await mocks.downloadClient.stub(DownloadClientStubs.sabQueueStub());

    const res = await api.downloadClient.test(
      buildDownloadClientPayload('sabnzbd', {
        name: 'sab-resolve-conn',
        host: TEST_CONFIG.mocks.downloadClientUrl,
        password: '\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022',
        apiKey: 'new-key',
        clientId: created.id,
      }),
    );
    expect(res.ok).toBe(true);

    await api.downloadClient.delete(created.id);
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

  test('test connection with Unknown typeName returns 400', async ({ api }) => {
    const payload = buildDownloadClientPayload('qbittorrent', {
      name: 'test-unknown',
      host: TEST_CONFIG.mocks.downloadClientUrl,
      username: 'admin',
      password: 'admin',
      typeName: 'Unknown',
    });
    const res = await api.downloadClient.test(payload);
    expect(res.status).toBe(400);
  });

  test('test connection with undefined typeName (42) returns 400', async ({ api }) => {
    const payload = buildDownloadClientPayload('qbittorrent', {
      name: 'test-42',
      host: TEST_CONFIG.mocks.downloadClientUrl,
      username: 'admin',
      password: 'admin',
      typeName: '42',
    });
    const res = await api.downloadClient.test(payload);
    expect(res.status).toBe(400);
  });
});

/**
 * Against the real qBittorrent container rather than WireMock, since the api-key auth
 * path (AuthController::apiKeySessionInitialize) bypasses isAuthNeeded()'s LocalHostAuth /
 * AuthSubnetWhitelist check entirely: a wrong key gets no session and a real 403, even from
 * a request originating on 127.0.0.0/8 under this stack's network_mode: host.
 */
test.describe('DownloadClient: test connection (real qBittorrent container)', () => {
  test('qbittorrent: the seeded api key succeeds', async ({ api }) => {
    const res = await api.downloadClient.test(
      buildDownloadClientPayload('qbittorrent', {
        name: 'qb-live-key-conn',
        host: 'http://localhost:8090',
        apiKey: TEST_CONFIG.liveQbittorrent.apiKey,
      }),
    );
    expect(res.ok).toBe(true);
  });

  test('qbittorrent: a wrong api key fails', async ({ api }) => {
    const res = await api.downloadClient.test(
      buildDownloadClientPayload('qbittorrent', {
        name: 'qb-live-key-conn-wrong',
        host: 'http://localhost:8090',
        apiKey: 'qbt_1111111111111111111111111111',
      }),
    );
    expect(res.ok).toBe(false);
  });

  // The seeded config accepts any localhost login until this test disables the bypass.
  test('qbittorrent: a wrong password fails', async ({ api }) => {
    const driver = new QBittorrentDriver();
    await driver.ready();
    await driver.setLocalAuthBypass(false);
    try {
      const res = await api.downloadClient.test(
        buildDownloadClientPayload('qbittorrent', {
          name: 'qb-live-wrong-password-conn',
          host: 'http://localhost:8090',
          username: 'admin',
          password: 'not-the-password',
        }),
      );
      expect(res.ok).toBe(false);
    } finally {
      await driver.setLocalAuthBypass(true);
    }
  });
});

test.describe('DownloadClient: test connection (real Transmission container)', () => {
  test('transmission: the seeded credentials succeed', async ({ api }) => {
    const driver = new TransmissionDriver();
    const res = await api.downloadClient.test(
      buildDownloadClientPayload('transmission', {
        name: 'tr-live-conn',
        host: driver.cleanuparrHost,
        username: driver.username,
        password: driver.password,
      }),
    );
    expect(res.ok).toBe(true);
  });

  test('transmission: a wrong password fails', async ({ api }) => {
    const driver = new TransmissionDriver();
    const res = await api.downloadClient.test(
      buildDownloadClientPayload('transmission', {
        name: 'tr-live-wrong-password-conn',
        host: driver.cleanuparrHost,
        username: driver.username,
        password: 'not-the-password',
      }),
    );
    expect(res.ok).toBe(false);
  });
});
