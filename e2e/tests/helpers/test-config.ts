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

  mocks: {
    arrUrl: 'http://localhost:9100',
    downloadClientUrl: 'http://localhost:9200',
    notifyUrl: 'http://localhost:9300',
    blocklistUrl: 'http://localhost:9400',
    arrAdminUrl: 'http://localhost:9100/__admin',
    downloadClientAdminUrl: 'http://localhost:9200/__admin',
    notifyAdminUrl: 'http://localhost:9300/__admin',
    blocklistAdminUrl: 'http://localhost:9400/__admin',
  },
} as const;
