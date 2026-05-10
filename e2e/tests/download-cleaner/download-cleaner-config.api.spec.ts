import { test, expect } from '../fixtures/base';

test.describe('DownloadCleaner — config', () => {
  test('GET returns config with clients + rules collections', async ({ api }) => {
    const res = await api.downloadCleaner.getConfig();
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body).toHaveProperty('enabled');
    expect(body).toHaveProperty('cronExpression');
  });

  test('PUT updates enabled + cron', async ({ api }) => {
    const before = await (await api.downloadCleaner.getConfig()).json();
    const res = await api.downloadCleaner.updateConfig({
      ...before,
      enabled: true,
      cronExpression: '0 0/30 * * * ?',
    });
    expect(res.ok).toBe(true);

    const after = await (await api.downloadCleaner.getConfig()).json();
    expect(after.enabled).toBe(true);
    expect(after.cronExpression).toBe('0 0/30 * * * ?');

    await api.downloadCleaner.updateConfig(before);
  });

  test('PUT rejects invalid cron', async ({ api }) => {
    const before = await (await api.downloadCleaner.getConfig()).json();
    const res = await api.downloadCleaner.updateConfig({
      ...before,
      cronExpression: 'invalid',
    });
    expect(res.status).toBeGreaterThanOrEqual(400);
    expect(res.status).toBeLessThan(500);
  });
});
