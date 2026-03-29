import { TorrentPrivacyType } from './enums';

export interface CleanCategory {
  name: string;
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
  downloadDirectorySource: string | null;
  downloadDirectoryTarget: string | null;
}

export interface ClientCleanerConfig {
  downloadClientId: string;
  downloadClientName: string;
  downloadClientTypeName: string;
  seedingRules: CleanCategory[];
  unlinkedConfig: UnlinkedConfigModel | null;
}

export interface DownloadCleanerConfig {
  enabled: boolean;
  cronExpression: string;
  useAdvancedScheduling: boolean;
  ignoredDownloads: string[];
  clients: ClientCleanerConfig[];
}

export function createDefaultCategory(): CleanCategory {
  return {
    name: '',
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
    downloadDirectorySource: null,
    downloadDirectoryTarget: null,
  };
}
