import { TEST_CONFIG } from './test-config';

const KC = TEST_CONFIG.keycloakUrl;
const REALM = TEST_CONFIG.realm;

export async function getAdminToken(): Promise<string> {
  const res = await fetch(`${KC}/realms/master/protocol/openid-connect/token`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      grant_type: 'password',
      client_id: 'admin-cli',
      username: 'admin',
      password: 'admin',
    }),
  });
  if (!res.ok) throw new Error(`Failed to get admin token: ${res.status}`);
  const data = await res.json();
  return data.access_token;
}

export async function getSubjectForUser(username: string): Promise<string> {
  const token = await getAdminToken();
  const res = await fetch(
    `${KC}/admin/realms/${REALM}/users?username=${encodeURIComponent(username)}&exact=true`,
    { headers: { Authorization: `Bearer ${token}` } },
  );
  if (!res.ok) throw new Error(`Failed to get user: ${res.status}`);
  const users = await res.json();
  if (!users.length) throw new Error(`User '${username}' not found in Keycloak`);
  return users[0].id;
}

export async function waitForKeycloak(timeoutMs = 90_000): Promise<void> {
  const start = Date.now();
  const discoveryUrl = `${KC}/realms/${REALM}/.well-known/openid-configuration`;

  while (Date.now() - start < timeoutMs) {
    try {
      const res = await fetch(discoveryUrl);
      if (res.ok) return;
    } catch {
      // Not ready yet
    }
    await new Promise((r) => setTimeout(r, 2000));
  }
  throw new Error(`Keycloak did not become ready within ${timeoutMs}ms`);
}
