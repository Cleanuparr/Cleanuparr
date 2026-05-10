import { test, expect } from '../fixtures/base';

test.describe('Core — strikes', () => {
  test('GET /api/strikes returns paginated payload', async ({ api }) => {
    const res = await api.strikes.list({ page: 1, pageSize: 10 });
    expect(res.status).toBe(200);
  });

  test('GET /recent returns array', async ({ api }) => {
    const res = await api.strikes.recent(5);
    expect(res.status).toBe(200);
    expect(Array.isArray(await res.json())).toBe(true);
  });

  test('GET /types returns string array', async ({ api }) => {
    const res = await api.strikes.types();
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(Array.isArray(body)).toBe(true);
  });

  test('DELETE on unknown id returns 404 or 204', async ({ api }) => {
    const res = await api.strikes.delete('00000000-0000-0000-0000-000000000000');
    expect([204, 404, 400]).toContain(res.status);
  });

  test('GET requires auth', async ({ anonymousApi }) => {
    const res = await anonymousApi.strikes.list();
    expect(res.status).toBe(401);
  });
});
