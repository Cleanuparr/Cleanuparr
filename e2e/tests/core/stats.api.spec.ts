import { test, expect } from '../fixtures/base';

test.describe('Core — stats', () => {
  test('GET /api/stats returns aggregate', async ({ api }) => {
    const res = await api.stats.get();
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(typeof body).toBe('object');
  });

  test('GET supports hours parameter', async ({ api }) => {
    const res = await api.stats.get({ hours: 1 });
    expect(res.status).toBe(200);
  });

  test('GET with includeEvents=1 includes recent events', async ({ api }) => {
    const res = await api.stats.get({ includeEvents: 1 });
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body).toHaveProperty('recentEvents');
  });

  test('GET with includeStrikes=1 includes recent strikes', async ({ api }) => {
    const res = await api.stats.get({ includeStrikes: 1 });
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body).toHaveProperty('recentStrikes');
  });

  test('GET requires auth', async ({ anonymousApi }) => {
    const res = await anonymousApi.stats.get();
    expect(res.status).toBe(401);
  });
});
