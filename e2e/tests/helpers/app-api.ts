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
    exclusiveMode: boolean;
  }>,
): Promise<void> {
  const getRes = await fetch(`${API}/api/account/oidc`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  });
  if (!getRes.ok) throw new Error(`Failed to get OIDC config: ${getRes.status}`);

  const currentConfig = await getRes.json();

  const putRes = await fetch(`${API}/api/account/oidc`, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${accessToken}`,
    },
    body: JSON.stringify({ ...currentConfig, ...updates }),
  });
  if (!putRes.ok) {
    const body = await putRes.text();
    throw new Error(`Failed to update OIDC config: ${putRes.status} ${body}`);
  }
}

// --- Seeker API helpers ---

export async function getSeekerConfig(accessToken: string): Promise<Response> {
  return fetch(`${API}/api/configuration/seeker`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  });
}

export async function updateSeekerConfig(
  accessToken: string,
  config: Record<string, unknown>,
): Promise<Response> {
  return fetch(`${API}/api/configuration/seeker`, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${accessToken}`,
    },
    body: JSON.stringify(config),
  });
}

export async function getSearchStatsSummary(accessToken: string): Promise<Response> {
  return fetch(`${API}/api/seeker/search-stats/summary`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  });
}

export async function getSearchEvents(accessToken: string): Promise<Response> {
  return fetch(`${API}/api/seeker/search-stats/events`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  });
}

export async function getCfScores(
  accessToken: string,
  params?: Record<string, string>,
): Promise<Response> {
  const query = params ? '?' + new URLSearchParams(params).toString() : '';
  return fetch(`${API}/api/seeker/cf-scores${query}`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  });
}

export async function getCfScoreStats(accessToken: string): Promise<Response> {
  return fetch(`${API}/api/seeker/cf-scores/stats`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  });
}

// --- Arr Instance helpers ---

export async function createArrInstance(
  accessToken: string,
  type: 'sonarr' | 'radarr',
  instance: { name: string; url: string; apiKey: string; version: number },
): Promise<Response> {
  return fetch(`${API}/api/configuration/${type}/instances`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${accessToken}`,
    },
    body: JSON.stringify(instance),
  });
}

export async function deleteArrInstance(
  accessToken: string,
  type: 'sonarr' | 'radarr',
  instanceId: string,
): Promise<Response> {
  return fetch(`${API}/api/configuration/${type}/instances/${instanceId}`, {
    method: 'DELETE',
    headers: { Authorization: `Bearer ${accessToken}` },
  });
}

// --- Download Cleaner API helpers ---

export async function getDownloadCleanerConfig(accessToken: string): Promise<Response> {
  return fetch(`${API}/api/configuration/download_cleaner`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  });
}

export async function updateDownloadCleanerConfig(
  accessToken: string,
  config: Record<string, unknown>,
): Promise<Response> {
  return fetch(`${API}/api/configuration/download_cleaner`, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${accessToken}`,
    },
    body: JSON.stringify(config),
  });
}

export async function getSeedingRules(accessToken: string, downloadClientId: string): Promise<Response> {
  return fetch(`${API}/api/seeding-rules/${downloadClientId}`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  });
}

export async function createSeedingRule(
  accessToken: string,
  downloadClientId: string,
  rule: Record<string, unknown>,
): Promise<Response> {
  return fetch(`${API}/api/seeding-rules/${downloadClientId}`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${accessToken}`,
    },
    body: JSON.stringify(rule),
  });
}

export async function updateSeedingRule(
  accessToken: string,
  ruleId: string,
  rule: Record<string, unknown>,
): Promise<Response> {
  return fetch(`${API}/api/seeding-rules/${ruleId}`, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${accessToken}`,
    },
    body: JSON.stringify(rule),
  });
}

export async function deleteSeedingRule(accessToken: string, ruleId: string): Promise<Response> {
  return fetch(`${API}/api/seeding-rules/${ruleId}`, {
    method: 'DELETE',
    headers: { Authorization: `Bearer ${accessToken}` },
  });
}

export async function getUnlinkedConfig(accessToken: string, downloadClientId: string): Promise<Response> {
  return fetch(`${API}/api/unlinked-config/${downloadClientId}`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  });
}

export async function updateUnlinkedConfig(
  accessToken: string,
  downloadClientId: string,
  config: Record<string, unknown>,
): Promise<Response> {
  return fetch(`${API}/api/unlinked-config/${downloadClientId}`, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${accessToken}`,
    },
    body: JSON.stringify(config),
  });
}

export async function reorderSeedingRules(
  accessToken: string,
  downloadClientId: string,
  orderedIds: string[],
): Promise<Response> {
  return fetch(`${API}/api/seeding-rules/${downloadClientId}/reorder`, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${accessToken}`,
    },
    body: JSON.stringify({ orderedIds }),
  });
}

// --- Download Client helpers ---

export async function createDownloadClient(
  accessToken: string,
  client: Record<string, unknown>,
): Promise<Response> {
  return fetch(`${API}/api/configuration/download_client`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${accessToken}`,
    },
    body: JSON.stringify(client),
  });
}

export async function deleteDownloadClient(accessToken: string, clientId: string): Promise<Response> {
  return fetch(`${API}/api/configuration/download_client/${clientId}`, {
    method: 'DELETE',
    headers: { Authorization: `Bearer ${accessToken}` },
  });
}

// --- General config / auth-bypass helpers ---

export async function getGeneralConfig(accessToken: string): Promise<Record<string, unknown>> {
  const res = await fetch(`${API}/api/configuration/general`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  });
  if (!res.ok) {
    throw new Error(`Failed to GET general config: ${res.status}`);
  }
  return res.json();
}

export async function updateGeneralConfig(
  accessToken: string,
  config: Record<string, unknown>,
): Promise<void> {
  const res = await fetch(`${API}/api/configuration/general`, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${accessToken}`,
    },
    body: JSON.stringify(config),
  });
  if (!res.ok) {
    const body = await res.text();
    throw new Error(`Failed to PUT general config: ${res.status} ${body}`);
  }
}

interface AuthBypassOptions {
  disableAuthForLocalAddresses: boolean;
  trustForwardedHeaders: boolean;
  trustedNetworks: string[];
}

export async function setAuthBypass(
  accessToken: string,
  opts: AuthBypassOptions,
): Promise<void> {
  const current = await getGeneralConfig(accessToken);
  await updateGeneralConfig(accessToken, {
    ...current,
    auth: {
      disableAuthForLocalAddresses: opts.disableAuthForLocalAddresses,
      trustForwardedHeaders: opts.trustForwardedHeaders,
      trustedNetworks: opts.trustedNetworks,
    },
  });
}

export async function configureOidc(accessToken: string): Promise<void> {
  const putRes = await fetch(`${API}/api/account/oidc`, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${accessToken}`,
    },
    body: JSON.stringify({
      enabled: true,
      issuerUrl: `${TEST_CONFIG.keycloakUrl}/realms/${TEST_CONFIG.realm}`,
      clientId: TEST_CONFIG.clientId,
      clientSecret: TEST_CONFIG.clientSecret,
      scopes: 'openid profile email',
      providerName: TEST_CONFIG.oidcProviderName,
    }),
  });
  if (!putRes.ok) {
    const body = await putRes.text();
    throw new Error(`Failed to configure OIDC: ${putRes.status} ${body}`);
  }
}
