import { test, expect, TEST_CONFIG, CleanuparrApi } from '../fixtures/base';

const RENAMED = 'renamed-admin';

let usernameChanged = false;

/** Logs in past the short lockout a rejected attempt leaves behind. */
async function loginWhenUnlocked(username: string): Promise<string | null> {
  let accessToken: string | null = null;

  await expect
    .poll(async () => {
      const res = await new CleanuparrApi().auth.login(username, TEST_CONFIG.adminPassword);
      if (!res.ok) {
        return res.status;
      }
      accessToken = (await res.json()).tokens.accessToken;
      return 200;
    }, { timeout: 15_000 })
    .toBe(200);

  return accessToken;
}

// Runs last in the account folder, and still restores the admin username so a
// retry starts from the same state.
test.describe.serial('Account — change username', () => {
  test.afterAll(async () => {
    if (!usernameChanged) {
      return;
    }

    const accessToken = await loginWhenUnlocked(RENAMED);
    expect(accessToken, 'failed to log in as the renamed admin to restore the username').toBeTruthy();

    const restored = await new CleanuparrApi({ token: accessToken! }).account.changeUsername(
      TEST_CONFIG.adminPassword,
      TEST_CONFIG.adminUsername,
    );
    expect(restored.ok, 'failed to restore the admin username').toBe(true);
    usernameChanged = false;
  });

  test('requires auth', async ({ anonymousApi }) => {
    const res = await anonymousApi.account.changeUsername(TEST_CONFIG.adminPassword, RENAMED);
    expect(res.status).toBe(401);
  });

  test('rejects a wrong password and keeps the username', async ({ api }) => {
    const res = await api.account.changeUsername('not-the-password', RENAMED);
    expect(res.status).toBe(400);

    const body = await (await api.account.get()).json();
    expect(body.username).toBe(TEST_CONFIG.adminUsername);
  });

  test('rejects a username shorter than three characters', async ({ api }) => {
    const res = await api.account.changeUsername(TEST_CONFIG.adminPassword, 'ab');
    expect(res.status).toBe(400);
  });

  test('rejects the current username', async ({ api }) => {
    const res = await api.account.changeUsername(TEST_CONFIG.adminPassword, TEST_CONFIG.adminUsername);
    expect(res.status).toBe(400);
  });

  test('renames the admin, revokes the old sessions and moves login to the new name', async ({ api, anonymousApi }) => {
    const before = await api.auth.loginAndCaptureTokens(TEST_CONFIG.adminUsername, TEST_CONFIG.adminPassword);

    const res = await api.account.changeUsername(TEST_CONFIG.adminPassword, `  ${RENAMED}  `);
    expect(res.status).toBe(200);
    usernameChanged = true;

    const body = await (await api.account.get()).json();
    expect(body.username).toBe(RENAMED);

    const refreshed = await anonymousApi.auth.refresh(before.refreshToken);
    expect(refreshed.ok).toBe(false);

    const oldName = await anonymousApi.auth.login(TEST_CONFIG.adminUsername, TEST_CONFIG.adminPassword);
    expect(oldName.status).toBe(401);

    expect(await loginWhenUnlocked(RENAMED)).toBeTruthy();
  });
});
