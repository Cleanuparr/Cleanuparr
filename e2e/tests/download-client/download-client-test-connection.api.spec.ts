import { test, expect } from '../fixtures/base';
import { DownloadClientStubs } from '../helpers/mocks';

test.describe('DownloadClient — test connection', () => {
  test('qbittorrent: success when login returns Ok.', async ({ api, mocks }) => {
    await mocks.downloadClient.stub(DownloadClientStubs.qbitVersionStub());
    await mocks.downloadClient.stub(DownloadClientStubs.qbitLoginOkStub());
    const res = await api.downloadClient.test({
      name: 'qb-conn',
      type: 'qbittorrent',
      host: 'localhost',
      port: 9200,
      username: 'admin',
      password: 'admin',
    });
    expect(res.ok).toBe(true);
  });

  test('qbittorrent: failure when login returns Fails.', async ({ api, mocks }) => {
    await mocks.downloadClient.stub(DownloadClientStubs.qbitLoginFailStub());
    const res = await api.downloadClient.test({
      name: 'qb-conn-bad',
      type: 'qbittorrent',
      host: 'localhost',
      port: 9200,
      username: 'admin',
      password: 'wrong',
    });
    expect(res.ok).toBe(false);
  });

  test('transmission: success on RPC handshake', async ({ api, mocks }) => {
    await mocks.downloadClient.stub(DownloadClientStubs.transmissionSessionStub());
    const res = await api.downloadClient.test({
      name: 'tr-conn',
      type: 'transmission',
      host: 'localhost',
      port: 9200,
      username: 'admin',
      password: 'admin',
    });
    expect(res.ok).toBe(true);
  });

  test('deluge: success on auth.login', async ({ api, mocks }) => {
    await mocks.downloadClient.stub(DownloadClientStubs.delugeLoginStub());
    const res = await api.downloadClient.test({
      name: 'dl-conn',
      type: 'deluge',
      host: 'localhost',
      port: 9200,
      username: '',
      password: 'admin',
    });
    expect(res.ok).toBe(true);
  });

  test('utorrent: success on token.html', async ({ api, mocks }) => {
    await mocks.downloadClient.stub(DownloadClientStubs.utorrentTokenStub());
    const res = await api.downloadClient.test({
      name: 'ut-conn',
      type: 'utorrent',
      host: 'localhost',
      port: 9200,
      username: 'admin',
      password: 'admin',
    });
    expect(res.ok).toBe(true);
  });

  test('rtorrent: success on XML-RPC', async ({ api, mocks }) => {
    await mocks.downloadClient.stub(DownloadClientStubs.rtorrentXmlRpcStub());
    const res = await api.downloadClient.test({
      name: 'rt-conn',
      type: 'rtorrent',
      host: 'localhost',
      port: 9200,
      username: 'admin',
      password: 'admin',
      urlBase: '/RPC2',
    });
    expect(res.ok).toBe(true);
  });

  test('any client: failure when host unreachable', async ({ api }) => {
    const res = await api.downloadClient.test({
      name: 'unreachable',
      type: 'qbittorrent',
      host: '127.0.0.1',
      port: 1,
      username: 'a',
      password: 'b',
    });
    expect(res.ok).toBe(false);
  });
});
