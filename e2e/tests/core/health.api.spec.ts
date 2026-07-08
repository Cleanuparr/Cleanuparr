import { test, expect } from '../fixtures/base';

test.describe('Core — health endpoints', () => {
  test('GET /health returns liveness payload (anonymous)', async ({ anonymousApi }) => {
    const res = await anonymousApi.health.liveness();
    expect(res.status).toBe(200);
    const body = await res.text();
    expect(body.length).toBeGreaterThan(0);
  });

  test('GET /health/ready returns readiness payload (anonymous)', async ({ anonymousApi }) => {
    const res = await anonymousApi.health.readiness();
    expect(res.status).toBe(200);
  });

  test('GET /health/detailed returns full health info', async ({ api }) => {
    const res = await api.health.detailed();
    expect(res.status).toBe(200);
  });

  test('GET /api/health returns download client health array', async ({ api }) => {
    const res = await api.health.downloadClients();
    expect(res.status).toBe(200);
  });

  test('POST /api/health/check triggers full check', async ({ api }) => {
    const res = await api.health.triggerCheck();
    expect(res.status).toBeLessThan(300);
  });

  test('GET /api/health requires auth', async ({ anonymousApi }) => {
    const res = await anonymousApi.health.downloadClients();
    expect(res.status).toBe(401);
  });
});
