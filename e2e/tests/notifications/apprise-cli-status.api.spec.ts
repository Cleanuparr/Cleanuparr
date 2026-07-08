import { test, expect } from '../fixtures/base';

test.describe('Notifications — Apprise CLI status', () => {
  test('GET returns availability flag', async ({ api }) => {
    const res = await api.notifications.appriseCliStatus();
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(typeof body.available).toBe('boolean');
  });

  test('GET requires auth', async ({ anonymousApi }) => {
    const res = await anonymousApi.notifications.appriseCliStatus();
    expect(res.status).toBe(401);
  });
});
