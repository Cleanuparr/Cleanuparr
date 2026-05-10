<<<<<<<< HEAD:e2e/tests/oidc-subject-mismatch.spec.ts
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
========
import { test, expect, TEST_CONFIG } from '../fixtures/base';
import { createKeycloakUser, deleteKeycloakUser } from '../helpers/keycloak';
>>>>>>>> e131fe85 (migrated OIDC specs (00-09, 15) to tests/oidc/):e2e/tests/oidc/05-subject-mismatch.ui.spec.ts

const WRONG_USER = 'wronguser';
const WRONG_PASS = 'wrongpass';
const WRONG_EMAIL = 'wronguser@example.com';

<<<<<<<< HEAD:e2e/tests/oidc-subject-mismatch.spec.ts
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

========
test.describe.serial('OIDC — subject mismatch', () => {
  test.beforeAll(async () => {
>>>>>>>> e131fe85 (migrated OIDC specs (00-09, 15) to tests/oidc/):e2e/tests/oidc/05-subject-mismatch.ui.spec.ts
    await createKeycloakUser(WRONG_USER, WRONG_PASS, WRONG_EMAIL);
  });

  test.afterAll(async () => {
    await deleteKeycloakUser(WRONG_USER);
    await clearOidcLink(token);
    await setOidcConfig(token, snapshot);
  });

  test('OIDC login with wrong Keycloak user shows unauthorized error', async ({ page }) => {
    await page.goto(`${TEST_CONFIG.appUrl}/auth/login`);
<<<<<<<< HEAD:e2e/tests/oidc-subject-mismatch.spec.ts

    await page.getByRole('button', { name: /sign in with/i }).click();

========
    await page.getByRole('button', { name: /sign in with/i }).click();
>>>>>>>> e131fe85 (migrated OIDC specs (00-09, 15) to tests/oidc/):e2e/tests/oidc/05-subject-mismatch.ui.spec.ts
    await expect(page).toHaveURL(/localhost:8080/, { timeout: 10_000 });

    await page.locator('#username').waitFor({ state: 'visible', timeout: 5_000 });
    await page.locator('#username').fill(WRONG_USER);
    await page.locator('#password').fill(WRONG_PASS);
    await page.locator('#kc-login').click();

<<<<<<<< HEAD:e2e/tests/oidc-subject-mismatch.spec.ts
    await expect(page).toHaveURL(/oidc_error=unauthorized/, {
      timeout: 15_000,
    });

========
    await expect(page).toHaveURL(/oidc_error=unauthorized/, { timeout: 15_000 });
>>>>>>>> e131fe85 (migrated OIDC specs (00-09, 15) to tests/oidc/):e2e/tests/oidc/05-subject-mismatch.ui.spec.ts
    await expect(page.locator('.error-message')).toHaveText(
      'Your account is not authorized for OIDC login',
    );
  });
});
