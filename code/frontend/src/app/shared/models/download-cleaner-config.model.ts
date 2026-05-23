import { TorrentPrivacyType } from './enums';

export interface SeedingRule {
  id?: string;
  name: string;
  categories: string[];
  trackerPatterns: string[];
  tagsAny?: string[];
  tagsAll?: string[];
  priority: number;
  privacyType: TorrentPrivacyType;
  maxRatio: number;
  minSeedTime: number;
  maxSeedTime: number;
  deleteSourceFiles: boolean;
}

export interface UnlinkedConfigModel {
  enabled: boolean;
  targetCategory: string;
  useTag: boolean;
  ignoredRootDirs: string[];
  categories: string[];
}

export interface OrphanedFilesClientConfig {
  enabled: boolean;
  scanDirectories: string[];
  orphanedDirectory?: string;
}

export interface ClientOrphanedFilesConfig {
  downloadClientId: string;
  downloadClientName: string;
  downloadClientEnabled: boolean;
  clientConfig: OrphanedFilesClientConfig | null;
}

export interface OrphanedFilesCleanerConfig {
  excludePatterns: string[];
  minFileAgeMinutes: number;
  emptyAfterXDays?: number;
  clients: ClientOrphanedFilesConfig[];
}

export interface ClientCleanerConfig {
  downloadClientId: string;
  downloadClientName: string;
  downloadClientEnabled: boolean;
  downloadClientTypeName: string;
  seedingRules: SeedingRule[];
  unlinkedConfig: UnlinkedConfigModel | null;
  orphanedFilesConfig: OrphanedFilesClientConfig | null;
}

export interface DownloadCleanerConfig {
  enabled: boolean;
  cronExpression: string;
  useAdvancedScheduling: boolean;
  ignoredDownloads: string[];
  clients: ClientCleanerConfig[];
}

export function createDefaultSeedingRule(): SeedingRule {
  return {
    name: '',
    categories: [],
    trackerPatterns: [],
    tagsAny: [],
    tagsAll: [],
    priority: 0,
    privacyType: TorrentPrivacyType.Public,
    maxRatio: -1,
    minSeedTime: 0,
    maxSeedTime: -1,
    deleteSourceFiles: true,
  };
}

export function createDefaultUnlinkedConfig(): UnlinkedConfigModel {
  return {
    enabled: false,
    targetCategory: 'cleanuparr-unlinked',
    useTag: false,
    ignoredRootDirs: [],
    categories: [],
  };
}

export function createDefaultOrphanedFilesClientConfig(): OrphanedFilesClientConfig {
  return {
    enabled: false,
    scanDirectories: [],
  };
}
