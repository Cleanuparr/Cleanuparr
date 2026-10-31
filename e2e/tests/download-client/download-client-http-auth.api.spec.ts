import { test, expect, TEST_CONFIG } from '../fixtures/base';
import { buildDownloadClientPayload } from '../helpers/api/download-client';
import { DownloadClientStubs } from '../helpers/mocks';

const PLACEHOLDER = '••••••••';

const PROXY = TEST_CONFIG.authProxy;

test.describe('DownloadClient — HTTP authentication (Basic/Digest proxy)', () => {
  test('qbittorrent: basic proxy + correct creds succeeds', async ({ api, mocks }) => {
    await mocks.downloadClient.stub(DownloadClientStubs.qbitVersionStub());
    await mocks.downloadClient.stub(DownloadClientStubs.qbitLoginOkStub());
    const res = await api.downloadClient.test(
      buildDownloadClientPayload('qbittorrent', {
        name: 'qb-basic',
        host: PROXY.basicUrl,
        username: 'admin',
        password: 'admin',
        httpAuthType: 'Basic',
        httpAuthUsername: PROXY.username,
        httpAuthPassword: PROXY.password,
      }),
    );
    expect(res.ok).toBe(true);
  });

  test('qbittorrent: digest proxy + correct creds + digest type succeeds', async ({ api, mocks }) => {
    await mocks.downloadClient.stub(DownloadClientStubs.qbitVersionStub());
    await mocks.downloadClient.stub(DownloadClientStubs.qbitLoginOkStub());
    const res = await api.downloadClient.test(
      buildDownloadClientPayload('qbittorrent', {
        name: 'qb-digest',
        host: PROXY.digestUrl,
        username: 'admin',
        password: 'admin',
        httpAuthType: 'Digest',
        httpAuthUsername: PROXY.username,
        httpAuthPassword: PROXY.password,
      }),
    );
    expect(res.ok).toBe(true);
  });

  test('qbittorrent: digest proxy answered with basic type fails', async ({ api, mocks }) => {
    await mocks.downloadClient.stub(DownloadClientStubs.qbitVersionStub());
    await mocks.downloadClient.stub(DownloadClientStubs.qbitLoginOkStub());
    const res = await api.downloadClient.test(
      buildDownloadClientPayload('qbittorrent', {
        name: 'qb-digest-wrong-type',
        host: PROXY.digestUrl,
        username: 'admin',
        password: 'admin',
        httpAuthType: 'Basic',
        httpAuthUsername: PROXY.username,
        httpAuthPassword: PROXY.password,
      }),
    );
    expect(res.ok).toBe(false);
  });

  test('qbittorrent: wrong proxy creds fail', async ({ api, mocks }) => {
    await mocks.downloadClient.stub(DownloadClientStubs.qbitVersionStub());
    await mocks.downloadClient.stub(DownloadClientStubs.qbitLoginOkStub());
    const res = await api.downloadClient.test(
      buildDownloadClientPayload('qbittorrent', {
        name: 'qb-digest-wrong-creds',
        host: PROXY.digestUrl,
        username: 'admin',
        password: 'admin',
        httpAuthType: 'Digest',
        httpAuthUsername: PROXY.username,
        httpAuthPassword: 'not-the-password',
      }),
    );
    expect(res.ok).toBe(false);
  });

  test('deluge: digest proxy + correct creds succeeds', async ({ api, mocks }) => {
    await mocks.downloadClient.stub(DownloadClientStubs.delugeLoginStub());
    const res = await api.downloadClient.test(
      buildDownloadClientPayload('deluge', {
        name: 'dl-digest',
        host: PROXY.digestUrl,
        password: 'admin',
        httpAuthType: 'Digest',
        httpAuthUsername: PROXY.username,
        httpAuthPassword: PROXY.password,
      }),
    );
    expect(res.ok).toBe(true);
  });

  // rTorrent and uTorrent reuse Username/Password as proxy credentials.
  test('rtorrent: basic proxy using Username/Password succeeds', async ({ api, mocks }) => {
    await mocks.downloadClient.stub(DownloadClientStubs.rtorrentXmlRpcStub());
    const res = await api.downloadClient.test(
      buildDownloadClientPayload('rtorrent', {
        name: 'rt-basic',
        host: PROXY.basicUrl,
        username: PROXY.username,
        password: PROXY.password,
        urlBase: '/RPC2',
        httpAuthType: 'Basic',
      }),
    );
    expect(res.ok).toBe(true);
  });

  test('rtorrent: digest proxy using Username/Password succeeds', async ({ api, mocks }) => {
    await mocks.downloadClient.stub(DownloadClientStubs.rtorrentXmlRpcStub());
    const res = await api.downloadClient.test(
      buildDownloadClientPayload('rtorrent', {
        name: 'rt-digest',
        host: PROXY.digestUrl,
        username: PROXY.username,
        password: PROXY.password,
        urlBase: '/RPC2',
        httpAuthType: 'Digest',
      }),
    );
    expect(res.ok).toBe(true);
  });

  test('rtorrent: digest proxy with basic type fails', async ({ api, mocks }) => {
    await mocks.downloadClient.stub(DownloadClientStubs.rtorrentXmlRpcStub());
    const res = await api.downloadClient.test(
      buildDownloadClientPayload('rtorrent', {
        name: 'rt-digest-wrong-type',
        host: PROXY.digestUrl,
        username: PROXY.username,
        password: PROXY.password,
        urlBase: '/RPC2',
        httpAuthType: 'Basic',
      }),
    );
    expect(res.ok).toBe(false);
  });

  test('transmission: basic proxy using Username/Password succeeds', async ({ api, mocks }) => {
    await mocks.downloadClient.stubMany(DownloadClientStubs.transmissionSessionGetStubs());
    const res = await api.downloadClient.test(
      buildDownloadClientPayload('transmission', {
        name: 'tr-basic',
        host: PROXY.basicUrl,
        urlBase: 'transmission',
        username: PROXY.username,
        password: PROXY.password,
        httpAuthType: 'Basic',
      }),
    );
    expect(res.ok).toBe(true);
  });

  test('transmission: digest proxy using Username/Password succeeds', async ({ api, mocks }) => {
    await mocks.downloadClient.stubMany(DownloadClientStubs.transmissionSessionGetStubs());
    const res = await api.downloadClient.test(
      buildDownloadClientPayload('transmission', {
        name: 'tr-digest',
        host: PROXY.digestUrl,
        urlBase: 'transmission',
        username: PROXY.username,
        password: PROXY.password,
        httpAuthType: 'Digest',
      }),
    );
    expect(res.ok).toBe(true);
  });

  test('transmission: digest proxy with basic type fails', async ({ api, mocks }) => {
    await mocks.downloadClient.stubMany(DownloadClientStubs.transmissionSessionGetStubs());
    const res = await api.downloadClient.test(
      buildDownloadClientPayload('transmission', {
        name: 'tr-digest-wrong-type',
        host: PROXY.digestUrl,
        urlBase: 'transmission',
        username: PROXY.username,
        password: PROXY.password,
        httpAuthType: 'Basic',
      }),
    );
    expect(res.ok).toBe(false);
  });

  test('transmission: wrong proxy creds fail', async ({ api, mocks }) => {
    await mocks.downloadClient.stubMany(DownloadClientStubs.transmissionSessionGetStubs());
    const res = await api.downloadClient.test(
      buildDownloadClientPayload('transmission', {
        name: 'tr-digest-wrong-creds',
        host: PROXY.digestUrl,
        urlBase: 'transmission',
        username: PROXY.username,
        password: 'not-the-password',
        httpAuthType: 'Digest',
      }),
    );
    expect(res.ok).toBe(false);
  });

  test('utorrent: digest proxy using Username/Password succeeds', async ({ api, mocks }) => {
    await mocks.downloadClient.stubMany(DownloadClientStubs.utorrentStubs());
    const res = await api.downloadClient.test(
      buildDownloadClientPayload('utorrent', {
        name: 'ut-digest',
        host: PROXY.digestUrl,
        username: PROXY.username,
        password: PROXY.password,
        httpAuthType: 'Digest',
      }),
    );
    expect(res.ok).toBe(true);
  });

  test('sabnzbd: apiKey combined with httpAuthUsername is accepted through the basic proxy', async ({
    api,
    mocks,
  }) => {
    await mocks.downloadClient.stub(DownloadClientStubs.sabQueueStub());
    const res = await api.downloadClient.test(
      buildDownloadClientPayload('sabnzbd', {
        name: 'sab-basic-plus-key',
        host: PROXY.basicUrl,
        apiKey: 'right-key',
        httpAuthType: 'Basic',
        httpAuthUsername: PROXY.username,
        httpAuthPassword: PROXY.password,
      }),
    );
    expect(res.ok).toBe(true);
  });

  test('sabnzbd: digest proxy + correct creds succeeds', async ({ api, mocks }) => {
    await mocks.downloadClient.stub(DownloadClientStubs.sabQueueStub());
    const res = await api.downloadClient.test(
      buildDownloadClientPayload('sabnzbd', {
        name: 'sab-digest',
        host: PROXY.digestUrl,
        apiKey: 'right-key',
        httpAuthType: 'Digest',
        httpAuthUsername: PROXY.username,
        httpAuthPassword: PROXY.password,
      }),
    );
    expect(res.ok).toBe(true);
  });

  test('qbittorrent: apiKey combined with httpAuthUsername is rejected with the conflict message', async ({
    api,
  }) => {
    const res = await api.downloadClient.test(
      buildDownloadClientPayload('qbittorrent', {
        name: 'qb-key-plus-http-auth',
        host: TEST_CONFIG.mocks.downloadClientUrl,
        apiKey: 'right-key',
        httpAuthUsername: PROXY.username,
        httpAuthPassword: PROXY.password,
      }),
    );
    expect(res.status).toBe(400);
    const body = await res.json();
    expect(body.detail ?? body.title ?? body.message).toContain(
      "API key can't be combined with HTTP authentication",
    );
  });

  test('host with URL userinfo is rejected with the credentials-in-url message', async ({ api }) => {
    const res = await api.downloadClient.test(
      buildDownloadClientPayload('qbittorrent', {
        name: 'qb-userinfo-host',
        host: 'http://admin:admin@localhost:9200',
        username: 'admin',
        password: 'admin',
      }),
    );
    expect(res.status).toBe(400);
    const body = await res.json();
    expect(body.detail ?? body.title ?? body.message).toContain('Credentials in the URL are not supported');
  });

  test('CRUD: httpAuthType/httpAuthUsername persist, httpAuthPassword stays masked and survives a placeholder update', async ({
    api,
    mocks,
  }) => {
    await mocks.downloadClient.stub(DownloadClientStubs.qbitVersionStub());
    await mocks.downloadClient.stub(DownloadClientStubs.qbitLoginOkStub());

    const created = await (
      await api.downloadClient.create(
        buildDownloadClientPayload('qbittorrent', {
          name: 'qb-http-auth-crud',
          host: PROXY.digestUrl,
          username: 'admin',
          password: 'admin',
          httpAuthType: 'Digest',
          httpAuthUsername: PROXY.username,
          httpAuthPassword: PROXY.password,
        }),
      )
    ).json();

    expect(created.httpAuthType).toBe('Digest');
    expect(created.httpAuthUsername).toBe(PROXY.username);
    expect(created.httpAuthPassword).toBe(PLACEHOLDER);

    const update = await (
      await api.downloadClient.update(created.id, {
        name: 'qb-http-auth-crud-renamed',
        host: PROXY.digestUrl,
        username: 'admin',
        password: 'admin',
        typeName: 'qBittorrent',
        httpAuthType: 'Digest',
        httpAuthUsername: PROXY.username,
        httpAuthPassword: PLACEHOLDER,
      })
    ).json();

    expect(update.httpAuthUsername).toBe(PROXY.username);
    expect(update.httpAuthPassword).toBe(PLACEHOLDER);

    // The placeholder kept the stored password, so the digest proxy still accepts it.
    const retest = await api.downloadClient.test(
      buildDownloadClientPayload('qbittorrent', {
        name: 'qb-http-auth-crud-renamed',
        host: PROXY.digestUrl,
        username: 'admin',
        password: 'admin',
        httpAuthType: 'Digest',
        httpAuthUsername: PROXY.username,
        httpAuthPassword: PLACEHOLDER,
        clientId: created.id,
      }),
    );
    expect(retest.ok).toBe(true);

    await api.downloadClient.delete(created.id);
  });
});
