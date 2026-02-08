import { PatternMode, ScheduleUnit } from './enums';
import { StallRule, SlowRule } from './queue-rule.model';

export interface JobSchedule {
  every: number;
  type: ScheduleUnit;
}

export const ScheduleOptions: Record<ScheduleUnit, number[]> = {
  [ScheduleUnit.Seconds]: [30],
  [ScheduleUnit.Minutes]: [1, 2, 3, 4, 5, 6, 10, 12, 15, 20, 30],
  [ScheduleUnit.Hours]: [1, 2, 3, 4, 6],
};

export interface FailedImportConfig {
  maxStrikes: number;
  ignorePrivate: boolean;
  deletePrivate: boolean;
  skipIfNotFoundInClient: boolean;
  patterns: string[];
  patternMode?: PatternMode;
}

export interface QueueCleanerConfig {
  enabled: boolean;
  cronExpression: string;
  useAdvancedScheduling: boolean;
  jobSchedule?: JobSchedule;
  ignoredDownloads: string[];
  failedImport: FailedImportConfig;
  downloadingMetadataMaxStrikes: number;
  stallRules?: StallRule[];
  slowRules?: SlowRule[];
}
