import { test, expect } from '../fixtures/base';
import { expectKeys } from '../helpers/contract';

test.describe('General config', () => {
  test('GET returns the singleton config', async ({ api }) => {
    const res = await api.general.getConfig();
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body).toHaveProperty('dryRun');
    expect(body).toHaveProperty('ignoredDownloads');
    expect(body).toHaveProperty('log');
    expect(body).toHaveProperty('auth');
  });

  test('GET response shape is pinned', async ({ api }) => {
    const body = await api.general.getJsonConfig();
    expectKeys(body, [
      'auth', 'connectivityCheckEnabled', 'connectivityCheckUrls', 'displaySupportBanner',
      'dryRun', 'historyRetentionDays', 'httpCertificateValidation',
      'httpMaxRetries', 'httpSendUserAgent', 'httpTimeout', 'ignoredDownloads', 'log',
      'statusCheckEnabled', 'strikeInactivityWindowHours',
    ]);
    expectKeys(body.log, [
      'archiveEnabled', 'archiveRetainedCount', 'archiveTimeLimitHours', 'level',
      'retainedFileCount', 'rollingSizeMB', 'timeLimitHours',
    ]);
    expectKeys(body.auth, ['disableAuthForLocalAddresses', 'trustForwardedHeaders', 'trustedNetworks']);
  });

  test('PUT toggles dry run', async ({ api }) => {
    const before = await api.general.getJsonConfig();
    const next = !before.dryRun;
    await api.general.patch({ dryRun: next });
    const after = await api.general.getJsonConfig();
    expect(after.dryRun).toBe(next);

    await api.general.patch({ dryRun: before.dryRun });
  });

  test('PUT updates log level', async ({ api }) => {
    const before = await api.general.getJsonConfig();
    await api.general.patch({ log: { ...(before.log as object), level: 'debug' } });
    const after = await api.general.getJsonConfig();
    expect((after.log as { level: string }).level.toLowerCase()).toBe('debug');

    await api.general.patch({ log: before.log });
  });

  test('PUT updates ignored downloads', async ({ api }) => {
    await api.general.patch({ ignoredDownloads: ['e2e-ignore-1', 'e2e-ignore-2'] });
    const after = await api.general.getJsonConfig();
    const ignored = after.ignoredDownloads as string[];
    expect(ignored).toEqual(expect.arrayContaining(['e2e-ignore-1', 'e2e-ignore-2']));

    await api.general.patch({ ignoredDownloads: [] });
  });

  test('GET requires auth', async ({ anonymousApi }) => {
    const res = await anonymousApi.general.getConfig();
    expect(res.status).toBe(401);
  });
});
