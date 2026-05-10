import { test, expect } from '../fixtures/base';

const TRIGGERABLE = ['QueueCleaner', 'MalwareBlocker', 'DownloadCleaner', 'BlacklistSync'] as const;

test.describe('Core — jobs', () => {
  test('GET /api/jobs returns array of job statuses', async ({ api }) => {
    const res = await api.jobs.list();
    expect(res.status).toBe(200);
    const body = await res.json();
    const jobs = Array.isArray(body) ? body : body.jobs ?? body.items ?? [];
    expect(Array.isArray(jobs)).toBe(true);
    expect(jobs.length).toBeGreaterThan(0);
  });

  test('GET /api/jobs/{type} returns single job info', async ({ api }) => {
    const res = await api.jobs.get('QueueCleaner');
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body).toHaveProperty('jobType');
  });

  for (const type of TRIGGERABLE) {
    test(`POST /trigger ${type} returns success`, async ({ api }) => {
      const res = await api.jobs.trigger(type);
      expect(res.status).toBeLessThan(300);
    });
  }

  test('POST /trigger Seeker is rejected', async ({ api }) => {
    const res = await api.jobs.trigger('Seeker');
    expect(res.status).toBeGreaterThanOrEqual(400);
    expect(res.status).toBeLessThan(500);
  });

  test('PUT /schedule rejects invalid interval', async ({ api }) => {
    const res = await api.jobs.updateSchedule('QueueCleaner', { every: 7, type: 'Minutes' });
    expect(res.status).toBeGreaterThanOrEqual(400);
    expect(res.status).toBeLessThan(500);
  });

  test('PUT /schedule accepts valid interval', async ({ api }) => {
    const res = await api.jobs.updateSchedule('QueueCleaner', { every: 30, type: 'Minutes' });
    expect(res.ok).toBe(true);
  });

  test('GET requires auth', async ({ anonymousApi }) => {
    const res = await anonymousApi.jobs.list();
    expect(res.status).toBe(401);
  });
});
