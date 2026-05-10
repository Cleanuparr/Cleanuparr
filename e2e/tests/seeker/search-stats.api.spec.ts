import { test, expect } from '../fixtures/base';

test.describe('Seeker — search stats', () => {
  test('GET /summary returns object with counters', async ({ api }) => {
    const res = await api.seeker.getSearchStatsSummary();
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(typeof body).toBe('object');
  });

  test('GET /events returns paginated array', async ({ api }) => {
    const res = await api.seeker.getSearchEvents({ page: '1', pageSize: '10' });
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(Array.isArray(body) || Array.isArray(body.items) || Array.isArray(body.records)).toBe(true);
  });

  test('GET /events accepts severity filter', async ({ api }) => {
    const res = await api.seeker.getSearchEvents({ severity: 'info' });
    expect(res.status).toBe(200);
  });

  test('GET requires auth', async ({ anonymousApi }) => {
    const res = await anonymousApi.seeker.getSearchStatsSummary();
    expect(res.status).toBe(401);
  });
});
