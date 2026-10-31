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

  // The real Sonarr, Radarr, Whisparr v3, Whisparr v2, Lidarr and Chaptarr containers.
  // Their api keys come from the committed seed in e2e/arr-seed.
  liveArr: {
    sonarrUrl: 'http://localhost:8989',
    sonarrApiKey: '0000000000000000000000000000e2e1',
    radarrUrl: 'http://localhost:7878',
    radarrApiKey: '0000000000000000000000000000e2e2',
    seededSeriesTitle: 'Agatha All Along',
    seededMovieTitle: 'F1',
    whisparrUrl: 'http://localhost:6970',
    whisparrApiKey: '0000000000000000000000000000e2e6',
    seededWhisparrTitle: 'Package Holiday',
    whisparr2Url: 'http://localhost:6971',
    whisparr2ApiKey: '0000000000000000000000000000e2e8',
    seededWhisparr2Title: 'Tushy',
    lidarrUrl: 'http://localhost:8686',
    lidarrApiKey: '0000000000000000000000000000e2e7',
    seededArtistName: 'Jeff Buckley',
    seededAlbumTitle: 'Grace',
    chaptarrUrl: 'http://localhost:8789',
    chaptarrApiKey: '0000000000000000000000000000e2e9',
    seededAuthorName: 'Delia Ray',
    seededBookTitle: 'Finding Fortune',
  },

  // The Whisparr v2 seed keeps two years of one site.
  // Every other year was pruned, and the scene titles are placeholders.
  whisparr2Seed: {
    siteId: 1,
    // All four scenes are monitored and missing: the Seeker searches the whole year.
    yearSearch: {
      year: 2019,
      scenes: [
        { id: 256, releaseDate: '2019-01-01' },
        { id: 257, releaseDate: '2019-01-06' },
        { id: 258, releaseDate: '2019-01-11' },
        { id: 259, releaseDate: '2019-01-16' },
      ],
    },
    // Only scene 183 is monitored and scene 184 has a file, so the Seeker searches scene 183 alone.
    singleScene: {
      year: 2018,
      monitored: { id: 183, releaseDate: '2018-01-01' },
      withFile: { id: 184, releaseDate: '2018-01-06' },
      unmonitored: { id: 185, releaseDate: '2018-01-11' },
    },
  },

  // Real qBittorrent container: the API key seeded into
  // test-data/qbittorrent-config/qBittorrent/qBittorrent.conf by setup-test-data.sh.
  liveQbittorrent: {
    apiKey: 'qbt_0000000000000000000000000e2e',
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
