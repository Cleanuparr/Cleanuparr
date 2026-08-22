import { test, expect, TEST_CONFIG, CleanuparrApi } from '../fixtures/base';
import { TokenResponse } from '../helpers/api/auth';
import { generateTotpCode } from '../helpers/totp';

interface TwoFactorSetup {
  secret: string;
  recoveryCodes: string[];
}

async function enableTwoFactor(api: CleanuparrApi): Promise<TwoFactorSetup> {
  const enable = await api.account.enable2fa(TEST_CONFIG.adminPassword);
  expect(enable.ok).toBe(true);

  const setup: TwoFactorSetup = await enable.json();
  expect(setup.recoveryCodes.length).toBeGreaterThan(1);

  const verify = await api.account.enable2faVerify(generateTotpCode(setup.secret));
  expect(verify.ok).toBe(true);

  return setup;
}

async function isTwoFactorEnabled(api: CleanuparrApi): Promise<boolean> {
  const account = await (await api.account.get()).json();
  return account.twoFactorEnabled;
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

/** Waits for the lockout to end. Password login shares the same counter. */
async function waitOutLockout(response: Response): Promise<void> {
  const body = await response.clone().json();
  const seconds = body.retryAfterSeconds ?? 2;
  await sleep(seconds * 1000 + 500);
}

/** Turns 2FA off again to keep the folder baseline when a test fails. */
async function forceDisable(api: CleanuparrApi, setup: TwoFactorSetup): Promise<void> {
  if (!(await isTwoFactorEnabled(api))) {
    return;
  }

  const first = await api.account.disable2fa(TEST_CONFIG.adminPassword, generateTotpCode(setup.secret));
  if (first.ok) {
    return;
  }

  await waitOutLockout(first);

  const second = await api.account.disable2fa(TEST_CONFIG.adminPassword, generateTotpCode(setup.secret));
  expect(second.ok).toBe(true);
}

/** Signs in with a recovery code and returns a client for that session. The code is used up. */
async function loginWithRecoveryCode(anonymousApi: CleanuparrApi, recoveryCode: string): Promise<CleanuparrApi> {
  const login = await anonymousApi.auth.login(TEST_CONFIG.adminUsername, TEST_CONFIG.adminPassword);
  expect(login.status).toBe(200);

  const loginBody = await login.json();
  expect(loginBody.requiresTwoFactor).toBe(true);

  const verify = await anonymousApi.auth.loginTwoFactor(loginBody.loginToken, recoveryCode, true);
  expect(verify.status).toBe(200);

  const tokens: TokenResponse = await verify.json();
  return new CleanuparrApi({ token: tokens.accessToken });
}

test.describe('Account — 2FA with recovery codes', () => {
  test('a recovery code disables 2FA after signing in with another one', async ({ api, anonymousApi }) => {
    const setup = await enableTwoFactor(api);

    try {
      expect(await isTwoFactorEnabled(api)).toBe(true);

      const recoveryApi = await loginWithRecoveryCode(anonymousApi, setup.recoveryCodes[0]);

      const disable = await recoveryApi.account.disable2fa(
        TEST_CONFIG.adminPassword,
        setup.recoveryCodes[1],
      );

      expect(disable.status).toBe(200);
      expect(await isTwoFactorEnabled(api)).toBe(false);
    } finally {
      await forceDisable(api, setup);
    }
  });

  test('a recovery code already consumed at login is rejected', async ({ api, anonymousApi }) => {
    const setup = await enableTwoFactor(api);

    try {
      await loginWithRecoveryCode(anonymousApi, setup.recoveryCodes[0]);

      const disable = await api.account.disable2fa(TEST_CONFIG.adminPassword, setup.recoveryCodes[0]);

      expect(disable.status).toBe(400);
      expect(await isTwoFactorEnabled(api)).toBe(true);
    } finally {
      await forceDisable(api, setup);
    }
  });

  test('a recovery code regenerates 2FA and the fresh codes work', async ({ api }) => {
    const setup = await enableTwoFactor(api);
    let current = setup;

    try {
      const regenerate = await api.account.regenerate2fa(
        TEST_CONFIG.adminPassword,
        setup.recoveryCodes[0],
      );
      expect(regenerate.status).toBe(200);

      current = await regenerate.json();
      expect(current.secret).not.toBe(setup.secret);
      expect(current.recoveryCodes).toHaveLength(10);
      expect(current.recoveryCodes).not.toContain(setup.recoveryCodes[0]);

      const disable = await api.account.disable2fa(TEST_CONFIG.adminPassword, current.recoveryCodes[0]);

      expect(disable.status).toBe(200);
      expect(await isTwoFactorEnabled(api)).toBe(false);
    } finally {
      await forceDisable(api, current);
    }
  });

  test('an authenticator code still works and an unknown code does not', async ({ api }) => {
    const setup = await enableTwoFactor(api);

    try {
      const rejected = await api.account.disable2fa(TEST_CONFIG.adminPassword, 'ZZZZ-ZZZZ');
      expect(rejected.status).toBe(400);
      expect(await isTwoFactorEnabled(api)).toBe(true);

      await waitOutLockout(rejected);

      const disable = await api.account.disable2fa(
        TEST_CONFIG.adminPassword,
        generateTotpCode(setup.secret),
      );
      expect(disable.status).toBe(200);
      expect(await isTwoFactorEnabled(api)).toBe(false);
    } finally {
      await forceDisable(api, setup);
    }
  });

  test('repeated bad codes are rate limited', async ({ api }) => {
    const setup = await enableTwoFactor(api);

    try {
      const first = await api.account.disable2fa(TEST_CONFIG.adminPassword, 'ZZZZ-ZZZZ');
      expect(first.status).toBe(400);
      expect((await first.clone().json()).retryAfterSeconds).toBeGreaterThan(0);

      const second = await api.account.disable2fa(TEST_CONFIG.adminPassword, 'ZZZZ-ZZZZ');
      expect(second.status).toBe(429);
      expect(await isTwoFactorEnabled(api)).toBe(true);

      await waitOutLockout(second);

      const disable = await api.account.disable2fa(
        TEST_CONFIG.adminPassword,
        generateTotpCode(setup.secret),
      );
      expect(disable.status).toBe(200);
    } finally {
      await forceDisable(api, setup);
    }
  });

  test('repeated bad codes at the 2FA login step are rate limited', async ({ api, anonymousApi }) => {
    const setup = await enableTwoFactor(api);

    try {
      const login = await anonymousApi.auth.login(TEST_CONFIG.adminUsername, TEST_CONFIG.adminPassword);
      expect(login.status).toBe(200);

      const loginToken = (await login.json()).loginToken;

      const first = await anonymousApi.auth.loginTwoFactor(loginToken, '000000');
      expect(first.status).toBe(401);
      expect((await first.clone().json()).retryAfterSeconds).toBeGreaterThan(0);

      const second = await anonymousApi.auth.loginTwoFactor(loginToken, '000000');
      expect(second.status).toBe(429);

      await waitOutLockout(second);
    } finally {
      await forceDisable(api, setup);
    }
  });
});
