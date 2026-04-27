import { test, expect } from '@playwright/test';
import { loginAndGetToken, setAuthBypass } from './helpers/app-api';
import { TEST_CONFIG } from './helpers/test-config';

// Regression for GHSA-8q44-v65j-jc3q

test.describe.serial('GHSA-8q44-v65j-jc3q regression', () => {
  let token: string;
  const PROXY = TEST_CONFIG.proxyUrl;
  const ATTACKER = `${PROXY}/attacker`;

  test.beforeAll(async () => {
    token = await loginAndGetToken();
    await setAuthBypass(token, {
      disableAuthForLocalAddresses: true,
      trustForwardedHeaders: true,
      trustedNetworks: [],
    });
  });

  test.afterAll(async () => {
    await setAuthBypass(token, {
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
