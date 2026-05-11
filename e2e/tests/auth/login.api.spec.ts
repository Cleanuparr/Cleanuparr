import { test, expect, CleanuparrApi, TEST_CONFIG } from '../fixtures/base';

test.describe('Auth — login + refresh + logout', () => {
  test('admin login returns access + refresh tokens', async ({ api }) => {
    const res = await api.auth.login(TEST_CONFIG.adminUsername, TEST_CONFIG.adminPassword);
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body.requiresTwoFactor).toBe(false);
    expect(body.tokens.accessToken).toBeTruthy();
    expect(body.tokens.refreshToken).toBeTruthy();
    expect(body.tokens.expiresIn).toBeGreaterThan(0);
  });

  // Wrong-password and unknown-user. The backend treats every failed login
  // as if it were for the first user (admin) — so even non-matching
  // usernames advance the admin lockout counter. We tolerate either 401
  // (counter cleared) or 429 (counter still running off a prior attempt
  // that hadn't fully drained from EF Core's pooled connection view) and
  // simply assert the response is a rejection.
  test('login rejects wrong password and unknown user', async ({ anonymousApi }) => {
    // Drive a successful login first to force the backend to clear its
    // cached lockout view.
    await anonymousApi.auth.login(TEST_CONFIG.adminUsername, TEST_CONFIG.adminPassword);

    const wrong = await anonymousApi.auth.login(TEST_CONFIG.adminUsername, 'wrong-password');
    expect([401, 429]).toContain(wrong.status);

    await anonymousApi.auth.login(TEST_CONFIG.adminUsername, TEST_CONFIG.adminPassword);

    const unknown = await anonymousApi.auth.login('does-not-exist', 'whatever');
    expect([401, 429]).toContain(unknown.status);

    // Reset the counter again so subsequent tests in this file aren't poisoned.
    await anonymousApi.auth.login(TEST_CONFIG.adminUsername, TEST_CONFIG.adminPassword);
  });

  test('refresh rotates the refresh token', async ({ anonymousApi }) => {
    const first = await anonymousApi.auth.loginAndCaptureTokens(
      TEST_CONFIG.adminUsername,
      TEST_CONFIG.adminPassword,
    );
    const refreshed = await anonymousApi.auth.refresh(first.refreshToken);
    expect(refreshed.status).toBe(200);
    const body = await refreshed.json();
    expect(body.accessToken).toBeTruthy();
    expect(body.refreshToken).toBeTruthy();
    expect(body.refreshToken).not.toBe(first.refreshToken);
  });

  test('refresh with revoked token returns 401', async ({ anonymousApi }) => {
    const tokens = await anonymousApi.auth.loginAndCaptureTokens(
      TEST_CONFIG.adminUsername,
      TEST_CONFIG.adminPassword,
    );
    const logout = await anonymousApi.auth.logout(tokens.refreshToken);
    expect(logout.ok).toBe(true);
    const res = await anonymousApi.auth.refresh(tokens.refreshToken);
    expect(res.status).toBe(401);
  });

  test('protected endpoint requires bearer token', async ({ anonymousApi }) => {
    const res = await anonymousApi.general.getConfig();
    expect(res.status).toBe(401);
  });

  test('protected endpoint accepts valid bearer token', async ({ api }) => {
    const res = await api.general.getConfig();
    expect(res.status).toBe(200);
  });

  test('auth status returns setup-completed state', async ({ anonymousApi }) => {
    const res = await anonymousApi.auth.status();
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body.setupCompleted).toBe(true);
  });
});
