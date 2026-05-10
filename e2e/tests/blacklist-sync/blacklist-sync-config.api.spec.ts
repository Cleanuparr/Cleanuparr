import { test, expect } from '../fixtures/base';

test.describe('BlacklistSync — config', () => {
  test('GET returns config singleton', async ({ api }) => {
    const res = await api.blacklistSync.getConfig();
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body).toHaveProperty('enabled');
    expect(body).toHaveProperty('cronExpression');
  });

  test('PUT toggles enabled + cron', async ({ api }) => {
    const before = await (await api.blacklistSync.getConfig()).json();
    const res = await api.blacklistSync.updateConfig({
      ...before,
      enabled: true,
      cronExpression: '0 0 0/6 * * ?',
    });
    expect(res.ok).toBe(true);

    const after = await (await api.blacklistSync.getConfig()).json();
    expect(after.enabled).toBe(true);
    expect(after.cronExpression).toBe('0 0 0/6 * * ?');

    await api.blacklistSync.updateConfig(before);
  });

  test('PUT rejects invalid cron', async ({ api }) => {
    const before = await (await api.blacklistSync.getConfig()).json();
    const res = await api.blacklistSync.updateConfig({
      ...before,
      cronExpression: 'not-a-cron',
    });
    expect(res.status).toBeGreaterThanOrEqual(400);
    expect(res.status).toBeLessThan(500);
  });
});
