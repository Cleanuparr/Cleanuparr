import { test, expect } from '@playwright/test';
import {
  loginAndGetToken,
  testDownloadClient,
  getGeneralConfig,
  updateGeneralConfig,
} from '../helpers/app-api';
import { TransmissionDriver } from '../helpers/torrent-clients/transmission';

const transmission = new TransmissionDriver();

const HTTP_TIMEOUT_SECONDS = 10;
const HTTP_MAX_RETRIES = 3;

function payload(): Record<string, unknown> {
  return {
    enabled: true,
    name: 'Transmission handshake e2e',
    typeName: transmission.typeName,
    type: 'Torrent',
    host: transmission.cleanuparrHost,
    username: transmission.username,
    password: transmission.password,
  };
}

test.describe.serial('Transmission session handshake', () => {
  let token: string;
  let originalGeneralConfig: Record<string, unknown>;

  test.beforeAll(async () => {
    token = await loginAndGetToken();
    await transmission.ready();

    originalGeneralConfig = await getGeneralConfig(token);
    await updateGeneralConfig(token, {
      ...originalGeneralConfig,
      httpTimeout: HTTP_TIMEOUT_SECONDS,
      httpMaxRetries: HTTP_MAX_RETRIES,
    });
  });

  test.afterAll(async () => {
    await updateGeneralConfig(token, originalGeneralConfig).catch(() => {});
  });

  test('connects without spending the request timeout on 409 retries', async () => {
    const startedAt = Date.now();
    const res = await testDownloadClient(token, payload());
    const elapsedMs = Date.now() - startedAt;

    expect(res.ok, `test connection failed: ${res.status} ${await res.text()}`).toBe(true);
    expect(
      elapsedMs,
      `handshake took ${elapsedMs}ms: the 409 session challenge is being retried with backoff`,
    ).toBeLessThan(5_000);
  });
});
