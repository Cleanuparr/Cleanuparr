import { test, expect } from '../fixtures/base';

test.describe.serial('DownloadCleaner — config', () => {
  test('returns default config with clients + ignored downloads', async ({ api }) => {
    const res = await api.downloadCleaner.getConfig();
    expect(res.status).toBe(200);

    const body = await res.json();
    expect(body).toHaveProperty('enabled');
    expect(body).toHaveProperty('cronExpression');
    expect(body).toHaveProperty('useAdvancedScheduling');
    expect(body).toHaveProperty('ignoredDownloads');
    expect(body).toHaveProperty('clients');
    expect(Array.isArray(body.clients)).toBe(true);
  });

  test('updates global download cleaner config', async ({ api }) => {
    const current = await (await api.downloadCleaner.getConfig()).json();

    const updateRes = await api.downloadCleaner.updateConfig({
      enabled: !current.enabled,
      cronExpression: current.cronExpression,
      useAdvancedScheduling: current.useAdvancedScheduling,
      ignoredDownloads: current.ignoredDownloads,
    });
    expect(updateRes.status).toBe(200);

    const updated = await (await api.downloadCleaner.getConfig()).json();
    expect(updated.enabled).toBe(!current.enabled);

    await api.downloadCleaner.updateConfig({
      enabled: current.enabled,
      cronExpression: current.cronExpression,
      useAdvancedScheduling: current.useAdvancedScheduling,
      ignoredDownloads: current.ignoredDownloads,
    });
  });

  test('updates ignored downloads list', async ({ api }) => {
    const current = await (await api.downloadCleaner.getConfig()).json();

    const updateRes = await api.downloadCleaner.updateConfig({
      enabled: current.enabled,
      cronExpression: current.cronExpression,
      useAdvancedScheduling: current.useAdvancedScheduling,
      ignoredDownloads: ['test-ignored-hash-123'],
    });
    expect(updateRes.status).toBe(200);

    const updated = await (await api.downloadCleaner.getConfig()).json();
    expect(updated.ignoredDownloads).toContain('test-ignored-hash-123');

    await api.downloadCleaner.updateConfig({
      enabled: current.enabled,
      cronExpression: current.cronExpression,
      useAdvancedScheduling: current.useAdvancedScheduling,
      ignoredDownloads: current.ignoredDownloads,
    });
  });

  test('rejects invalid cron expression', async ({ api }) => {
    const current = await (await api.downloadCleaner.getConfig()).json();
    const res = await api.downloadCleaner.updateConfig({
      enabled: current.enabled,
      cronExpression: 'not-a-valid-cron',
      useAdvancedScheduling: true,
      ignoredDownloads: current.ignoredDownloads,
    });
    expect(res.status).toBeGreaterThanOrEqual(400);
  });

  test('GET requires auth', async ({ anonymousApi }) => {
    const res = await anonymousApi.downloadCleaner.getConfig();
    expect(res.status).toBe(401);
  });
});
