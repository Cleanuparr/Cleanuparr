export const TEST_CONFIG = {
  appUrl: 'http://localhost:5000',
  proxyUrl: 'http://localhost:8000',
  keycloakUrl: 'http://localhost:8080',
  realm: 'cleanuparr-test',
  clientId: 'cleanuparr',
  clientSecret: 'test-secret',

  adminUsername: 'admin',
  adminPassword: 'E2eTestPassword123!',

  oidcUsername: 'testuser',
  oidcPassword: 'testpass',
  oidcProviderName: 'Keycloak',
} as const;
