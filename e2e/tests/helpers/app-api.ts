import { TEST_CONFIG } from './test-config';

const API = TEST_CONFIG.appUrl;

interface TokenResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
}

interface LoginResponse {
  requiresTwoFactor: boolean;
  loginToken?: string;
  tokens?: TokenResponse;
}

export async function waitForApp(timeoutMs = 90_000): Promise<void> {
  const start = Date.now();

  while (Date.now() - start < timeoutMs) {
    try {
      const res = await fetch(`${API}/health`);
      if (res.ok) return;
    } catch {
      // Not ready yet
    }
    await new Promise((r) => setTimeout(r, 2000));
  }
  throw new Error(`App did not become ready within ${timeoutMs}ms`);
}

export async function createAccountAndSetup(): Promise<void> {
  const createRes = await fetch(`${API}/api/auth/setup/account`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      username: TEST_CONFIG.adminUsername,
      password: TEST_CONFIG.adminPassword,
    }),
  });
  // 409 = controller says account exists; 403 = middleware says setup already completed
  if (!createRes.ok && createRes.status !== 409 && createRes.status !== 403) {
    throw new Error(`Failed to create account: ${createRes.status}`);
  }

  const completeRes = await fetch(`${API}/api/auth/setup/complete`, {
    method: 'POST',
  });
  if (!completeRes.ok && completeRes.status !== 409 && completeRes.status !== 403) {
    throw new Error(`Failed to complete setup: ${completeRes.status}`);
  }
}

export async function loginAndGetToken(): Promise<string> {
  const res = await fetch(`${API}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      username: TEST_CONFIG.adminUsername,
      password: TEST_CONFIG.adminPassword,
    }),
  });
  if (!res.ok) throw new Error(`Login failed: ${res.status}`);

  const data: LoginResponse = await res.json();
  if (data.requiresTwoFactor || !data.tokens) {
    throw new Error('Unexpected 2FA requirement in E2E test');
  }
  return data.tokens.accessToken;
}

export async function updateOidcConfig(
  accessToken: string,
  updates: Partial<{
    enabled: boolean;
    providerName: string;
    issuerUrl: string;
    clientId: string;
    clientSecret: string;
    scopes: string;
  }>,
): Promise<void> {
  const getRes = await fetch(`${API}/api/configuration/general`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  });
  if (!getRes.ok) throw new Error(`Failed to get config: ${getRes.status}`);

  const config = await getRes.json();
  config.auth = config.auth ?? {};
  config.auth.oidc = { ...config.auth.oidc, ...updates };

  const putRes = await fetch(`${API}/api/configuration/general`, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${accessToken}`,
    },
    body: JSON.stringify(config),
  });
  if (!putRes.ok) {
    const body = await putRes.text();
    throw new Error(`Failed to update OIDC config: ${putRes.status} ${body}`);
  }
}

export async function configureOidc(accessToken: string): Promise<void> {
  const getRes = await fetch(`${API}/api/configuration/general`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  });
  if (!getRes.ok) throw new Error(`Failed to get config: ${getRes.status}`);

  const config = await getRes.json();

  config.auth = config.auth ?? {};
  config.auth.oidc = {
    enabled: true,
    issuerUrl: `${TEST_CONFIG.keycloakUrl}/realms/${TEST_CONFIG.realm}`,
    clientId: TEST_CONFIG.clientId,
    clientSecret: TEST_CONFIG.clientSecret,
    scopes: 'openid profile email',
    providerName: TEST_CONFIG.oidcProviderName,
  };

  const putRes = await fetch(`${API}/api/configuration/general`, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${accessToken}`,
    },
    body: JSON.stringify(config),
  });
  if (!putRes.ok) {
    const body = await putRes.text();
    throw new Error(`Failed to configure OIDC: ${putRes.status} ${body}`);
  }
}
