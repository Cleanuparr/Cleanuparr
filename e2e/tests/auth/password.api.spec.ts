import { test, expect, TEST_CONFIG, CleanuparrApi } from '../fixtures/base';

test.describe('Account — change password', () => {
  test('changing password revokes refresh tokens and new password works', async ({ api, anonymousApi }) => {
    const original = TEST_CONFIG.adminPassword;
    const next = 'NewE2ePass123!@#';

    const tokens = await anonymousApi.auth.loginAndCaptureTokens(
      TEST_CONFIG.adminUsername,
      original,
    );

    const change = await api.account.changePassword(original, next);
    expect(change.ok).toBe(true);

    try {
      const refresh = await anonymousApi.auth.refresh(tokens.refreshToken);
      expect(refresh.status).toBe(401);

      const newLogin = await anonymousApi.auth.login(TEST_CONFIG.adminUsername, next);
      expect(newLogin.status).toBe(200);
    } finally {
      const restore = await anonymousApi.auth.loginAndCaptureTokens(TEST_CONFIG.adminUsername, next);
      const restoreApi = new CleanuparrApi({ token: restore.accessToken });
      await restoreApi.account.changePassword(next, original);
    }
  });

  test('changing password with wrong current password returns 400 or 401', async ({ api }) => {
    const res = await api.account.changePassword('definitely-not-the-password', 'whatever-new');
    expect(res.status).toBeGreaterThanOrEqual(400);
    expect(res.status).toBeLessThan(500);
  });
});
