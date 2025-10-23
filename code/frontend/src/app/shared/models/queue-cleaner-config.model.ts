// Import the rule types
import { StallRule, SlowRule } from './queue-rule.model';

export enum ScheduleUnit {
  Seconds = 'Seconds',
  Minutes = 'Minutes',
  Hours = 'Hours'
}

export enum PatternMode {
  Exclude = 'Exclude',
  Include = 'Include'
}

/**
 * Valid values for each schedule unit
 */
export const ScheduleOptions = {
  [ScheduleUnit.Seconds]: [30],
  [ScheduleUnit.Minutes]: [1, 2, 3, 4, 5, 6, 10, 12, 15, 20, 30],
  [ScheduleUnit.Hours]: [1, 2, 3, 4, 6]
};

export interface JobSchedule {
  every: number;
  type: ScheduleUnit;
}

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
  jobSchedule?: JobSchedule; // UI-only field, not sent to API
  ignoredDownloads: string[];
  failedImport: FailedImportConfig;
  downloadingMetadataMaxStrikes: number;
  
  // Queue Rules
  stallRules?: StallRule[];
  slowRules?: SlowRule[];
}
