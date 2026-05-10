import { test, expect } from '../fixtures/base';

test.describe('BlacklistSync — config', () => {
  test('GET returns config singleton', async ({ api }) => {
    const res = await api.blacklistSync.getConfig();
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body).toHaveProperty('enabled');
    expect(body).toHaveProperty('cronExpression');
  });

  test('PUT toggles enabled + cron (requires blacklistPath when enabled)', async ({ api }) => {
    const before = await (await api.blacklistSync.getConfig()).json();
    const res = await api.blacklistSync.updateConfig({
      ...before,
      enabled: true,
      cronExpression: '0 0 0/6 * * ?',
      blacklistPath: 'https://example.com/blacklist.txt',
    });
    expect(res.ok).toBe(true);

    const after = await (await api.blacklistSync.getConfig()).json();
    expect(after.enabled).toBe(true);
    expect(after.cronExpression).toBe('0 0 0/6 * * ?');

    await api.blacklistSync.updateConfig(before);
  });

  test('PUT rejects enabling without a blacklist path', async ({ api }) => {
    const before = await (await api.blacklistSync.getConfig()).json();
    const res = await api.blacklistSync.updateConfig({
      ...before,
      enabled: true,
      blacklistPath: null,
    });
    expect(res.status).toBeGreaterThanOrEqual(400);
    expect(res.status).toBeLessThan(500);
  });
});
