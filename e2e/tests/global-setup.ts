import { CleanuparrApi, ensureAdminAccount, waitForApp } from './helpers/api';
import { TEST_CONFIG } from './helpers/test-config';
import { MockServers } from './helpers/mocks/wiremock-client';
import { waitForKeycloak } from './helpers/keycloak';

async function configureOidcForLegacyTests(api: CleanuparrApi): Promise<void> {
  await api.account.patchOidcConfig({
    enabled: true,
    issuerUrl: `${TEST_CONFIG.keycloakUrl}/realms/${TEST_CONFIG.realm}`,
    clientId: TEST_CONFIG.clientId,
    clientSecret: TEST_CONFIG.clientSecret,
    scopes: 'openid profile email',
    providerName: TEST_CONFIG.oidcProviderName,
  });
}

async function globalSetup(): Promise<void> {
  console.log('Waiting for Keycloak...');
  await waitForKeycloak();
  console.log('Keycloak ready.');

  console.log('Waiting for app...');
  const api = new CleanuparrApi();
  await waitForApp(api.client);
  console.log('App ready.');

  console.log('Waiting for WireMock servers...');
  const mocks = new MockServers();
  await mocks.waitReady();
  console.log('WireMock ready.');

  console.log('Ensuring admin account + completing setup...');
  await ensureAdminAccount(api);

  const tokens = await api.auth.loginAndCaptureTokens(
    TEST_CONFIG.adminUsername,
    TEST_CONFIG.adminPassword,
  );
  api.setToken(tokens.accessToken);

  console.log('Configuring OIDC for legacy + OIDC tests...');
  await configureOidcForLegacyTests(api);

  console.log('Global setup complete.');
}

export default globalSetup;
