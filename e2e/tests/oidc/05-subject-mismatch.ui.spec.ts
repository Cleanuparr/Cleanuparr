import { test, expect, TEST_CONFIG } from '../fixtures/base';
import { createKeycloakUser, deleteKeycloakUser } from '../helpers/keycloak';

const WRONG_USER = 'wronguser';
const WRONG_PASS = 'wrongpass';
const WRONG_EMAIL = 'wronguser@example.com';

test.describe.serial('OIDC — subject mismatch', () => {
  test.beforeAll(async () => {
    await createKeycloakUser(WRONG_USER, WRONG_PASS, WRONG_EMAIL);
  });

  test.afterAll(async () => {
    await deleteKeycloakUser(WRONG_USER);
  });

  test('OIDC login with wrong Keycloak user shows unauthorized error', async ({ page }) => {
    await page.goto(`${TEST_CONFIG.appUrl}/auth/login`);
    await page.getByRole('button', { name: /sign in with/i }).click();
    await expect(page).toHaveURL(/localhost:8080/, { timeout: 10_000 });

    await page.locator('#username').waitFor({ state: 'visible', timeout: 5_000 });
    await page.locator('#username').fill(WRONG_USER);
    await page.locator('#password').fill(WRONG_PASS);
    await page.locator('#kc-login').click();

    await expect(page).toHaveURL(/oidc_error=unauthorized/, { timeout: 15_000 });
    await expect(page.locator('.error-message')).toHaveText(
      'Your account is not authorized for OIDC login',
    );
  });
});
