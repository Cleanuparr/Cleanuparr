import { test, expect } from '@playwright/test';
import { TEST_CONFIG } from './helpers/test-config';
import { loginViaBrowser } from './helpers/ui';

/**
 * A transient failure of the refresh endpoint (server restarting, network blip,
 * laptop waking offline) must not destroy the still-valid refresh token. Only a
 * definitive 401 should end the session. Regression guard for users being bounced
 * to login after leaving a tab open for a few hours.
 */
// Force the stored access token to look expired so the next load triggers a refresh.
async function expireAccessToken(page: import('@playwright/test').Page): Promise<void> {
  await page.evaluate(() => {
    const past = Math.floor(Date.now() / 1000) - 3600;
    const b64url = (obj: unknown) =>
      btoa(JSON.stringify(obj)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
    localStorage.setItem('access_token', `eyJhbGciOiJIUzI1NiJ9.${b64url({ exp: past })}.sig`);
  });
}

test.describe('Token refresh transient failure', () => {
  test('a transient refresh failure keeps the session recoverable', async ({ page }) => {
    await loginViaBrowser(page);

    const originalRefreshToken = await page.evaluate(() =>
      localStorage.getItem('refresh_token'),
    );
    expect(originalRefreshToken).toBeTruthy();

    // A transient miss must never tear down the session.
    let logoutCalled = false;
    await page.route('**/api/auth/logout', (route) => {
      logoutCalled = true;
      return route.fulfill({ status: 200, body: '{}' });
    });

    // Simulate the server briefly unavailable during a token refresh.
    await page.route('**/api/auth/refresh', (route) =>
      route.fulfill({ status: 503, body: '' }),
    );
    await expireAccessToken(page);

    await page.reload();
    await page.waitForURL(/\/auth\/login/, { timeout: 15_000 });

    const refreshTokenAfterFailure = await page.evaluate(() =>
      localStorage.getItem('refresh_token'),
    );
    expect(refreshTokenAfterFailure).toBe(originalRefreshToken);
    expect(logoutCalled).toBe(false);

    // Server recovers: returning to the app refreshes silently, no re-login.
    await page.unroute('**/api/auth/refresh');
    await page.goto(`${TEST_CONFIG.appUrl}/dashboard`);
    await expect(page).toHaveURL(/\/dashboard/, { timeout: 15_000 });
  });

  test('a definitive 401 refresh rejection ends the session', async ({ page }) => {
    await loginViaBrowser(page);

    await page.route('**/api/auth/refresh', (route) =>
      route.fulfill({ status: 401, body: '' }),
    );
    await expireAccessToken(page);

    await page.reload();
    await page.waitForURL(/\/auth\/login/, { timeout: 15_000 });

    const refreshToken = await page.evaluate(() => localStorage.getItem('refresh_token'));
    expect(refreshToken).toBeNull();
  });
});
