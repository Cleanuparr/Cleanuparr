import { test, expect } from '../fixtures/base';

test.describe('Core — status', () => {
  test('GET /api/status returns system info', async ({ api }) => {
    const res = await api.status.system();
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(typeof body).toBe('object');
  });

  test('GET /api/status/download-client returns clients array', async ({ api }) => {
    const res = await api.status.downloadClients();
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body).toHaveProperty('clients');
    expect(Array.isArray(body.clients)).toBe(true);
  });

  test('GET /api/status/arrs returns arr health', async ({ api }) => {
    const res = await api.status.arrs();
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body).toHaveProperty('sonarr');
    expect(body).toHaveProperty('radarr');
  });

  test('GET requires auth', async ({ anonymousApi }) => {
    const res = await anonymousApi.status.system();
    expect(res.status).toBe(401);
  });
});
