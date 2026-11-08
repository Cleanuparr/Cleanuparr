import { test, expect } from '../fixtures/base';

test.describe('Notifications — Apprise CLI status', () => {
  test('GET reports the bundled CLI as available', async ({ api }) => {
    const res = await api.notifications.appriseCliStatus();
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body.available).toBe(true);
    expect(body.version).toMatch(/^Apprise v\d+\.\d+\.\d+$/);
  });

  test('GET requires auth', async ({ anonymousApi }) => {
    const res = await anonymousApi.notifications.appriseCliStatus();
    expect(res.status).toBe(401);
  });
});
