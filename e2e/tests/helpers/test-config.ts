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
    indexerUrl: 'http://localhost:9500',
    arrAdminUrl: 'http://localhost:9100/__admin',
    downloadClientAdminUrl: 'http://localhost:9200/__admin',
    notifyAdminUrl: 'http://localhost:9300/__admin',
    blocklistAdminUrl: 'http://localhost:9400/__admin',
    indexerAdminUrl: 'http://localhost:9500/__admin',
  },

  // The real Sonarr and Radarr containers.
  // Their api keys come from the committed seed in e2e/arr-seed.
  liveArr: {
    sonarrUrl: 'http://localhost:8989',
    sonarrApiKey: '0000000000000000000000000000e2e1',
    radarrUrl: 'http://localhost:7878',
    radarrApiKey: '0000000000000000000000000000e2e2',
    seededSeriesTitle: 'Agatha All Along',
    seededMovieTitle: 'F1',
  },

  // The real LazyLibrarian container.
  // Its api keys and library come from the committed seed in e2e/lazylibrarian-seed.
  liveLazyLibrarian: {
    url: 'http://localhost:5299',
    apiKey: '0000000000000000000000000000e2e3',
    readOnlyApiKey: '0000000000000000000000000000e2e4',
  },
} as const;

/** A cron far enough out that the schedule never fires inside a spec. */
export function farFutureCron(): string {
  const minutes = (new Date().getUTCMinutes() + 30) % 60;
  return `0 ${minutes} * * * ?`;
}
