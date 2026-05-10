import { test, expect, TEST_CONFIG, CleanuparrApi } from '../fixtures/base';

// Regression for GHSA-8q44-v65j-jc3q — spoofed X-Forwarded-For / X-Real-IP must not
// trigger trusted-network auth bypass when the request originates from a
// non-trusted source proxy (the nginx /attacker location simulates the spoof).

async function withAdminApi(): Promise<CleanuparrApi> {
  const api = new CleanuparrApi();
  const tokens = await api.auth.loginAndCaptureTokens(
    TEST_CONFIG.adminUsername,
    TEST_CONFIG.adminPassword,
  );
  api.setToken(tokens.accessToken);
  return api;
}

test.describe.serial('GHSA-8q44-v65j-jc3q regression', () => {
  const PROXY = TEST_CONFIG.proxyUrl;
  const ATTACKER = `${PROXY}/attacker`;

  test.beforeAll(async () => {
    const api = await withAdminApi();
    await api.general.setAuthBypass({
      disableAuthForLocalAddresses: true,
      trustForwardedHeaders: true,
      trustedNetworks: [],
    });
  });

  test.afterAll(async () => {
    const api = await withAdminApi();
    await api.general.setAuthBypass({
      disableAuthForLocalAddresses: false,
      trustForwardedHeaders: false,
      trustedNetworks: [],
    });
  });

  test('rejects spoofed X-Forwarded-For from public-IP attacker', async ({ request }) => {
    const res = await request.get(`${ATTACKER}/api/auth/status`, {
      headers: { 'X-Forwarded-For': '10.0.0.5' },
    });
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body.authBypassActive).toBe(false);
  });

  test('rejects spoofed X-Real-IP from public-IP attacker', async ({ request }) => {
    const res = await request.get(`${ATTACKER}/api/auth/status`, {
      headers: { 'X-Real-IP': '10.0.0.5' },
    });
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body.authBypassActive).toBe(false);
  });

  test('legitimate localhost request still gets bypass via direct nginx', async ({ request }) => {
    const res = await request.get(`${PROXY}/api/auth/status`);
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body.authBypassActive).toBe(true);
  });
});
