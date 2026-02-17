import { ScheduleUnit, TorrentPrivacyType } from './enums';
import { JobSchedule } from './queue-cleaner-config.model';

export interface CleanCategory {
  name: string;
  privacyType: TorrentPrivacyType;
  maxRatio: number;
  minSeedTime: number;
  maxSeedTime: number;
  deleteSourceFiles: boolean;
}

export interface DownloadCleanerConfig {
  enabled: boolean;
  cronExpression: string;
  useAdvancedScheduling: boolean;
  jobSchedule?: JobSchedule;
  categories: CleanCategory[];
  ignoredDownloads: string[];
  unlinkedEnabled: boolean;
  unlinkedTargetCategory: string;
  unlinkedUseTag: boolean;
  unlinkedIgnoredRootDirs: string[];
  unlinkedCategories: string[];
}

export function createDefaultCategory(): CleanCategory {
  return {
    name: '',
    privacyType: TorrentPrivacyType.Both,
    maxRatio: -1,
    minSeedTime: 0,
    maxSeedTime: -1,
    deleteSourceFiles: true,
  };
}
