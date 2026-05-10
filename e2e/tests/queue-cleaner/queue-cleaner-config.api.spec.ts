import { test, expect } from '../fixtures/base';

test.describe('QueueCleaner — config', () => {
  test('GET returns the config singleton', async ({ api }) => {
    const res = await api.queueCleaner.getConfig();
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body).toHaveProperty('enabled');
    expect(body).toHaveProperty('cronExpression');
  });

  test('PUT updates enabled flag and cron', async ({ api }) => {
    const before = await (await api.queueCleaner.getConfig()).json();
    const res = await api.queueCleaner.updateConfig({
      ...before,
      enabled: true,
      cronExpression: '0 0/30 * * * ?',
    });
    expect(res.ok).toBe(true);

    const after = await (await api.queueCleaner.getConfig()).json();
    expect(after.enabled).toBe(true);
    expect(after.cronExpression).toBe('0 0/30 * * * ?');

    await api.queueCleaner.updateConfig(before);
  });

  test('PUT rejects invalid cron expression', async ({ api }) => {
    const before = await (await api.queueCleaner.getConfig()).json();
    const res = await api.queueCleaner.updateConfig({
      ...before,
      cronExpression: 'not-a-cron',
    });
    expect(res.status).toBeGreaterThanOrEqual(400);
    expect(res.status).toBeLessThan(500);
  });

  test('GET requires auth', async ({ anonymousApi }) => {
    const res = await anonymousApi.queueCleaner.getConfig();
    expect(res.status).toBe(401);
  });
});
