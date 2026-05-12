import { CleanuparrApi, waitForApp } from './helpers/api';
import { MockServers } from './helpers/mocks/wiremock-client';
import { waitForKeycloak } from './helpers/keycloak';

// Per-folder app restarts (in tests/_setup/*.setup.ts) handle admin creation
// and OIDC config. globalSetup only verifies the long-lived services that
// the harness depends on are reachable before any project runs.
async function globalSetup(): Promise<void> {
  console.log('Waiting for Keycloak...');
  await waitForKeycloak();
  console.log('Keycloak ready.');

  console.log('Waiting for app...');
  await waitForApp(new CleanuparrApi().client);
  console.log('App ready.');

  console.log('Waiting for WireMock servers...');
  await new MockServers().waitReady();
  console.log('WireMock ready.');

  console.log('Global setup complete.');
}

export default globalSetup;
