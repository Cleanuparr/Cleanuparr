import { test, expect, TEST_CONFIG } from '../fixtures/base';

test.describe('Account — OIDC config CRUD', () => {
  test.afterEach(async ({ api }) => {
    await api.account.patchOidcConfig({
      enabled: true,
      issuerUrl: `${TEST_CONFIG.keycloakUrl}/realms/${TEST_CONFIG.realm}`,
      clientId: TEST_CONFIG.clientId,
      clientSecret: TEST_CONFIG.clientSecret,
      scopes: 'openid profile email',
      providerName: TEST_CONFIG.oidcProviderName,
      exclusiveMode: false,
    });
  });

  test('GET returns the current OIDC config', async ({ api }) => {
    const res = await api.account.getOidcConfig();
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body).toHaveProperty('enabled');
    expect(body).toHaveProperty('issuerUrl');
    expect(body).toHaveProperty('clientId');
  });

  test('PUT persists provider name change', async ({ api }) => {
    await api.account.patchOidcConfig({ providerName: 'CustomName' });
    const after = await (await api.account.getOidcConfig()).json();
    expect(after.providerName).toBe('CustomName');
  });

  test('PUT can disable OIDC', async ({ api }) => {
    await api.account.patchOidcConfig({ enabled: false });
    const after = await (await api.account.getOidcConfig()).json();
    expect(after.enabled).toBe(false);
  });

  test('PUT requires auth', async ({ anonymousApi }) => {
    const res = await anonymousApi.account.updateOidcConfig({ enabled: true });
    expect(res.status).toBe(401);
  });
});
