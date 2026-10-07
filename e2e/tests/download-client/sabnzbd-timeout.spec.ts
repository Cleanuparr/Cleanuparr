import { test, expect } from '@playwright/test';
import {
  loginAndGetToken,
  testDownloadClient,
  getGeneralConfig,
  updateGeneralConfig,
} from '../helpers/app-api';
import { WireMockClient } from '../helpers/mocks/wiremock-client';
import { TEST_CONFIG } from '../helpers/test-config';

/**
 * A SABnzbd request that outlives the configured HttpClient.Timeout used to surface
 * as an unwrapped TaskCanceledException. Test-connection must still fail cleanly
 * (not hang and not crash) once SabnzbdClient wraps it with the same mode context
 * as the HttpRequestException path.
 */

const HTTP_TIMEOUT_SECONDS = 2;
const RESPONSE_DELAY_MS = 5_000;

test.describe.serial('SABnzbd request timeout', () => {
  const downloadClientMock = new WireMockClient(TEST_CONFIG.mocks.downloadClientAdminUrl);
  let token: string;
  let originalGeneralConfig: Record<string, unknown>;

  test.beforeAll(async () => {
    token = await loginAndGetToken();
    await downloadClientMock.waitReady();

    originalGeneralConfig = await getGeneralConfig(token);
    await updateGeneralConfig(token, {
      ...originalGeneralConfig,
      httpTimeout: HTTP_TIMEOUT_SECONDS,
      httpMaxRetries: 0,
    });
  });

  test.afterEach(async () => {
    await downloadClientMock.resetAll();
  });

  test.afterAll(async () => {
    await updateGeneralConfig(token, originalGeneralConfig).catch(() => {});
  });

  test('test-connection fails cleanly instead of hanging', async () => {
    await downloadClientMock.stub({
      request: { method: 'GET', urlPath: '/api', queryParameters: { mode: { equalTo: 'queue' } } },
      response: { status: 200, jsonBody: { queue: { slots: [] } }, fixedDelayMilliseconds: RESPONSE_DELAY_MS },
    });

    const startedAt = Date.now();
    const res = await testDownloadClient(token, {
      enabled: true,
      name: 'SABnzbd timeout e2e',
      typeName: 'Sabnzbd',
      host: TEST_CONFIG.mocks.downloadClientUrl,
      apiKey: 'e2e-key',
    });
    const elapsedMs = Date.now() - startedAt;

    expect(res.ok, 'test-connection must fail, not succeed, against a stalled client').toBe(false);
    expect(
      elapsedMs,
      `test-connection took ${elapsedMs}ms: it must fail around the configured timeout, not the response delay`,
    ).toBeLessThan(RESPONSE_DELAY_MS);
  });
});
