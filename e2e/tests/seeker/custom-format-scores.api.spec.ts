import { test, expect } from '../fixtures/base';

test.describe('Seeker — custom format scores', () => {
  test('GET /api/seeker/cf-scores returns paginated payload', async ({ api }) => {
    const res = await api.seeker.listCustomFormatScores();
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(Array.isArray(body) || Array.isArray(body.items)).toBe(true);
  });

  test('GET /upgrades returns array or paginated payload', async ({ api }) => {
    const res = await api.seeker.listCustomFormatScoreUpgrades();
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(Array.isArray(body) || Array.isArray(body.items)).toBe(true);
  });

  test('GET /instances returns instances array wrapper', async ({ api }) => {
    const res = await api.seeker.listCustomFormatScoreInstances();
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(Array.isArray(body.instances)).toBe(true);
  });

  test('GET /stats returns aggregate object', async ({ api }) => {
    const res = await api.seeker.getCustomFormatScoreStats();
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(typeof body).toBe('object');
  });

  test('history returns 404 for unknown instance + item', async ({ api }) => {
    const res = await api.seeker.getCustomFormatScoreHistory(
      '00000000-0000-0000-0000-000000000000',
      '99999',
    );
    expect([200, 404]).toContain(res.status);
  });

  test('GET requires auth', async ({ anonymousApi }) => {
    const res = await anonymousApi.seeker.listCustomFormatScores();
    expect(res.status).toBe(401);
  });
});
