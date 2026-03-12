import { test, expect } from '@playwright/test';
import { TEST_CONFIG } from './helpers/test-config';
import {
  createKeycloakUser,
  deleteKeycloakUser,
} from './helpers/keycloak';

const WRONG_USER = 'wronguser';
const WRONG_PASS = 'wrongpass';
const WRONG_EMAIL = 'wronguser@example.com';

test.describe.serial('OIDC Subject Mismatch', () => {
  test.beforeAll(async () => {
    await createKeycloakUser(WRONG_USER, WRONG_PASS, WRONG_EMAIL);
  });

  test.afterAll(async () => {
    await deleteKeycloakUser(WRONG_USER);
  });

  test('OIDC login with wrong Keycloak user shows unauthorized error', async ({
    page,
  }) => {
    await page.goto(`${TEST_CONFIG.appUrl}/auth/login`);

    // Click OIDC login button
    await page.getByRole('button', { name: /sign in with/i }).click();

    // Should redirect to Keycloak
    await expect(page).toHaveURL(/localhost:8080/, { timeout: 10_000 });

    // Wait for Keycloak login form to render, then log in as the wrong user
    await page.locator('#username').waitFor({ state: 'visible', timeout: 5_000 });
    await page.locator('#username').fill(WRONG_USER);
    await page.locator('#password').fill(WRONG_PASS);
    await page.locator('#kc-login').click();

    // Backend detects subject mismatch and redirects to login with error
    await expect(page).toHaveURL(/oidc_error=unauthorized/, {
      timeout: 15_000,
    });

    await expect(page.locator('.error-message')).toHaveText(
      'Your account is not authorized for OIDC login',
    );
  });
});
