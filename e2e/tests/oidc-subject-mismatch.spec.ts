import { test, expect } from '@playwright/test';
import { TEST_CONFIG } from './helpers/test-config';
import {
  clearOidcLink,
  configureOidc,
  getOidcConfig,
  loginAndGetToken,
  OidcConfigSnapshot,
  setOidcConfig,
} from './helpers/app-api';
import { linkOidcViaBrowser } from './helpers/oidc';
import {
  createKeycloakUser,
  deleteKeycloakUser,
} from './helpers/keycloak';

const WRONG_USER = 'wronguser';
const WRONG_PASS = 'wrongpass';
const WRONG_EMAIL = 'wronguser@example.com';

test.describe('OIDC Subject Mismatch', () => {
  let token: string;
  let snapshot: OidcConfigSnapshot;

  test.beforeAll(async ({ browser }) => {
    token = await loginAndGetToken();
    snapshot = await getOidcConfig(token);
    await configureOidc(token);
    await clearOidcLink(token);

    const setupPage = await browser.newPage();
    try {
      await linkOidcViaBrowser(setupPage);
    } finally {
      await setupPage.close();
    }

    await createKeycloakUser(WRONG_USER, WRONG_PASS, WRONG_EMAIL);
  });

  test.afterAll(async () => {
    await deleteKeycloakUser(WRONG_USER);
    await clearOidcLink(token);
    await setOidcConfig(token, snapshot);
  });

  test('OIDC login with wrong Keycloak user shows unauthorized error', async ({
    page,
  }) => {
    await page.goto(`${TEST_CONFIG.appUrl}/auth/login`);

    await page.getByRole('button', { name: /sign in with/i }).click();

    await expect(page).toHaveURL(/localhost:8080/, { timeout: 10_000 });

    await page.locator('#username').waitFor({ state: 'visible', timeout: 5_000 });
    await page.locator('#username').fill(WRONG_USER);
    await page.locator('#password').fill(WRONG_PASS);
    await page.locator('#kc-login').click();

    await expect(page).toHaveURL(/oidc_error=unauthorized/, {
      timeout: 15_000,
    });

    await expect(page.locator('.error-message')).toHaveText(
      'Your account is not authorized for OIDC login',
    );
  });
});
