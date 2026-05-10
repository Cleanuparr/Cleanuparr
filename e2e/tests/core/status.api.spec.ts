import { test, expect } from '../fixtures/base';

test.describe('Core — status', () => {
  test('GET /api/status returns system info', async ({ api }) => {
    const res = await api.status.system();
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(typeof body).toBe('object');
  });

  test('GET /api/status/download-client returns 200', async ({ api }) => {
    const res = await api.status.downloadClients();
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(typeof body).toBe('object');
  });

  test('GET /api/status/arrs returns arr-keyed health buckets', async ({ api }) => {
    const res = await api.status.arrs();
    expect(res.status).toBe(200);
    const body = await res.json();
    // Response uses PascalCase keys: { Sonarr: [], Radarr: [], Lidarr: [], ... }.
    const keys = Object.keys(body).map((k) => k.toLowerCase());
    expect(keys).toEqual(expect.arrayContaining(['sonarr', 'radarr']));
  });

  test('GET requires auth', async ({ anonymousApi }) => {
    const res = await anonymousApi.status.system();
    expect(res.status).toBe(401);
  });
});
