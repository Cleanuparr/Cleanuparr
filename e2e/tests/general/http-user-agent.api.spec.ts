import { test, expect, TEST_CONFIG } from '../fixtures/base';
import { buildDownloadClientPayload } from '../helpers/api/download-client';
import { ArrStubs, DownloadClientStubs } from '../helpers/mocks';
import type { LoggedRequest } from '../helpers/mocks';

const USER_AGENT = /^Cleanuparr(\/\d+\.\d+\.\d+)?$/;

function userAgentOf(request: LoggedRequest): string {
  const match = Object.entries(request.headers).find(
    ([name]) => name.toLowerCase() === 'user-agent',
  );
  if (!match) {
    return '';
  }
  const value = match[1];
  return Array.isArray(value) ? value.join(', ') : value;
}

test.describe('HTTP user agent', () => {
  test.afterEach(async ({ api }) => {
    await api.general.patch({ httpSendUserAgent: false });
  });

  test('arr requests carry the user agent when the setting is on', async ({ api, mocks }) => {
    await api.general.patch({ httpSendUserAgent: true });
    await mocks.arr.stub(ArrStubs.arrHealthStub());
    await mocks.arr.resetRequests();

    const res = await api.arr.testInstance('sonarr', {
      name: 'ua-arr',
      url: TEST_CONFIG.mocks.arrUrl,
      apiKey: 'good-key',
      version: 3,
    });
    expect(res.ok).toBe(true);

    const entry = await mocks.arr.waitForRequest({
      method: 'GET',
      urlPattern: '/api/v[0-9]+/system/status.*',
    });
    expect(userAgentOf(entry)).toMatch(USER_AGENT);
  });

  test('download client requests carry the user agent when the setting is on', async ({ api, mocks }) => {
    await api.general.patch({ httpSendUserAgent: true });
    await mocks.downloadClient.stub(DownloadClientStubs.delugeLoginStub());
    await mocks.downloadClient.resetRequests();

    const res = await api.downloadClient.test(
      buildDownloadClientPayload('deluge', {
        name: 'ua-deluge',
        host: TEST_CONFIG.mocks.downloadClientUrl,
        password: 'admin',
      }),
    );
    expect(res.ok).toBe(true);

    const entry = await mocks.downloadClient.waitForRequest({ method: 'POST' });
    expect(userAgentOf(entry)).toMatch(USER_AGENT);
  });

  test('the setting takes effect without restarting the app', async ({ api, mocks }) => {
    await mocks.arr.stub(ArrStubs.arrHealthStub());
    const instance = {
      name: 'ua-toggle',
      url: TEST_CONFIG.mocks.arrUrl,
      apiKey: 'good-key',
      version: 3,
    };

    await api.general.patch({ httpSendUserAgent: false });
    await mocks.arr.resetRequests();
    await api.arr.testInstance('sonarr', instance);

    const withoutHeader = await mocks.arr.waitForRequest({ method: 'GET' });
    expect(userAgentOf(withoutHeader)).not.toMatch(USER_AGENT);

    await api.general.patch({ httpSendUserAgent: true });
    await mocks.arr.resetRequests();
    await api.arr.testInstance('sonarr', instance);

    const withHeader = await mocks.arr.waitForRequest({ method: 'GET' });
    expect(userAgentOf(withHeader)).toMatch(USER_AGENT);
  });
});
