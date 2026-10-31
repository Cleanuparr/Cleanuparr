import type { ArrType } from '@shared/models/enums';
import type { ArrAppMeta } from './arr-app-meta.model';

export const ARR_TYPES: ArrType[] = [
  'sonarr',
  'radarr',
  'lidarr',
  'readarr',
  'chaptarr',
  'whisparr',
  'sportarr',
  'lazylibrarian',
];

export const ARR_APP_META: Record<ArrType, ArrAppMeta> = {
  sonarr: {
    displayName: 'Sonarr',
    versionOptions: [{ label: 'v4', value: 4 }],
    defaultPort: 8989,
    experimental: false,
    iconSrc: 'icons/ext/sonarr-light.svg',
  },
  radarr: {
    displayName: 'Radarr',
    versionOptions: [{ label: 'v6', value: 6 }],
    defaultPort: 7878,
    experimental: false,
    iconSrc: 'icons/ext/radarr-light.svg',
  },
  lidarr: {
    displayName: 'Lidarr',
    versionOptions: [{ label: 'v3', value: 3 }],
    defaultPort: 8686,
    experimental: false,
    iconSrc: 'icons/ext/lidarr-light.svg',
  },
  readarr: {
    displayName: 'Readarr',
    versionOptions: [{ label: 'v0.4', value: 0.4 }],
    defaultPort: 8787,
    experimental: false,
    iconSrc: 'icons/ext/readarr-light.svg',
  },
  chaptarr: {
    displayName: 'Chaptarr',
    versionOptions: [{ label: 'v0.9', value: 0.9 }],
    defaultPort: 8789,
    experimental: false,
    iconSrc: 'icons/ext/chaptarr-light.svg',
    featureId: 'chaptarr',
  },
  whisparr: {
    displayName: 'Whisparr',
    versionOptions: [{ label: 'v2', value: 2 }, { label: 'v3', value: 3 }],
    defaultPort: 6969,
    experimental: false,
    iconSrc: 'icons/ext/whisparr-light.svg',
  },
  sportarr: {
    displayName: 'Sportarr',
    versionOptions: [{ label: 'v3', value: 3 }],
    defaultPort: 1867,
    experimental: false,
    iconSrc: 'icons/ext/sportarr-light.svg',
  },
  lazylibrarian: {
    displayName: 'LazyLibrarian',
    versionOptions: [{ label: 'latest', value: 1 }],
    defaultPort: 5299,
    experimental: true,
    iconSrc: 'icons/ext/lazylibrarian-light.svg',
  },
};
