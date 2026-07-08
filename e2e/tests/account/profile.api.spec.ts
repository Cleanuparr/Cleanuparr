import { test, expect, TEST_CONFIG } from '../fixtures/base';

test.describe('Account — profile', () => {
  test('GET /api/account returns admin info', async ({ api }) => {
    const res = await api.account.get();
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body.username).toBe(TEST_CONFIG.adminUsername);
    expect(typeof body.twoFactorEnabled).toBe('boolean');
    expect(typeof body.plexLinked).toBe('boolean');
  });

  test('GET /api/account requires auth', async ({ anonymousApi }) => {
    const res = await anonymousApi.account.get();
    expect(res.status).toBe(401);
  });
});
