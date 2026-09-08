import { PatternMode } from './enums';

// Re-export for backward compatibility
export type { JobSchedule } from '@shared/utils/schedule.util';
export { ScheduleOptions } from '@shared/utils/schedule.util';

export interface FailedImportConfig {
  maxStrikes: number;
  ignorePrivate: boolean;
  deletePrivate: boolean;
  skipIfNotFoundInClient: boolean;
  patterns: string[];
  patternMode?: PatternMode;
  changeCategory: boolean;
  forceImport: boolean;
  forceImportMaxTries: number;
}

export interface QueueCleanerConfig {
  enabled: boolean;
  cronExpression: string;
  useAdvancedScheduling: boolean;
  ignoredDownloads: string[];
  processNoContentId: boolean;
  failedImport: FailedImportConfig;
  downloadingMetadataMaxStrikes: number;
}
