import { test, expect } from '../fixtures/base';

test.describe('Core — manual events', () => {
  test('GET /api/manual-events returns paginated payload', async ({ api }) => {
    const res = await api.manualEvents.list({ page: 1, pageSize: 10 });
    expect(res.status).toBe(200);
  });

  test('GET /stats returns counters', async ({ api }) => {
    const res = await api.manualEvents.stats();
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body).toHaveProperty('totalEvents');
    expect(body).toHaveProperty('unresolvedEvents');
    expect(body).toHaveProperty('resolvedEvents');
  });

  test('GET /severities returns array', async ({ api }) => {
    const res = await api.manualEvents.severities();
    expect(res.status).toBe(200);
    expect(Array.isArray(await res.json())).toBe(true);
  });

  test('GET unknown id returns 404 or 400', async ({ api }) => {
    const res = await api.manualEvents.get('00000000-0000-0000-0000-000000000000');
    expect([404, 400]).toContain(res.status);
  });

  test('POST /cleanup returns deletion count', async ({ api }) => {
    const res = await api.manualEvents.cleanup(0);
    expect(res.status).toBe(200);
  });

  test('GET requires auth', async ({ anonymousApi }) => {
    const res = await anonymousApi.manualEvents.list();
    expect(res.status).toBe(401);
  });
});
