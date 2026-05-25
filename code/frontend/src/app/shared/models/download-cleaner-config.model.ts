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

export interface OrphanedFilesConfig {
  enabled: boolean;
  scanDirectories: string[];
  orphanedDirectory: string;
  excludePatterns: string[];
  minFileAgeMinutes: number;
  emptyAfterXDays?: number;
}

export interface ClientCleanerConfig {
  downloadClientId: string;
  downloadClientName: string;
  downloadClientEnabled: boolean;
  downloadClientTypeName: string;
  seedingRules: SeedingRule[];
  unlinkedConfig: UnlinkedConfigModel | null;
  orphanedFilesConfig: OrphanedFilesConfig | null;
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

export function createDefaultOrphanedFilesConfig(): OrphanedFilesConfig {
  return {
    enabled: false,
    scanDirectories: [],
    orphanedDirectory: '',
    excludePatterns: [],
    minFileAgeMinutes: 0,
  };
}
