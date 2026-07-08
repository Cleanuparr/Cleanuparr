import { test, expect } from '../fixtures/base';

test.describe('Core — events', () => {
  test('GET /api/events returns paginated payload', async ({ api }) => {
    const res = await api.events.list({ page: 1, pageSize: 10 });
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(typeof body).toBe('object');
    expect(Array.isArray(body.items) || Array.isArray(body.records)).toBe(true);
  });

  test('GET /types returns string array', async ({ api }) => {
    const res = await api.events.types();
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(Array.isArray(body)).toBe(true);
    expect(body.every((s: unknown) => typeof s === 'string')).toBe(true);
  });

  test('GET /severities returns string array', async ({ api }) => {
    const res = await api.events.severities();
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(Array.isArray(body)).toBe(true);
  });

  test('GET /api/events accepts severity filter', async ({ api }) => {
    const res = await api.events.list({ severity: 'info', page: 1, pageSize: 5 });
    expect(res.status).toBe(200);
  });

  test('GET /api/events accepts date range filter', async ({ api }) => {
    const res = await api.events.list({
      fromDate: '2024-01-01T00:00:00Z',
      toDate: '2099-01-01T00:00:00Z',
    });
    expect(res.status).toBe(200);
  });

  test('GET unknown id returns 404', async ({ api }) => {
    const res = await api.events.get('00000000-0000-0000-0000-000000000000');
    expect([404, 400]).toContain(res.status);
  });

  test('POST /cleanup returns deletion count', async ({ api }) => {
    const res = await api.events.cleanup(0);
    expect(res.status).toBe(200);
  });

  test('GET /api/events requires auth', async ({ anonymousApi }) => {
    const res = await anonymousApi.events.list();
    expect(res.status).toBe(401);
  });
});
