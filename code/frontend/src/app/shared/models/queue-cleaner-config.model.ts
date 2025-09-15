export enum ScheduleUnit {
  Seconds = 'Seconds',
  Minutes = 'Minutes',
  Hours = 'Hours'
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
  ignoredPatterns: string[];
}

export interface StalledConfig {
  maxStrikes: number;
  resetStrikesOnProgress: boolean;
  ignorePrivate: boolean;
  deletePrivate: boolean;
  downloadingMetadataMaxStrikes: number;
}

export interface SlowConfig {
  maxStrikes: number;
  resetStrikesOnProgress: boolean;
  ignorePrivate: boolean;
  deletePrivate: boolean;
  minSpeed: string;
  maxTime: number;
  ignoreAboveSize: string;
}

export interface QueueCleanerConfig {
  enabled: boolean;
  cronExpression: string;
  useAdvancedScheduling: boolean;
  jobSchedule?: JobSchedule; // UI-only field, not sent to API
  ignoredDownloads: string[];
  failedImport: FailedImportConfig;
  stalled: StalledConfig;
  slow: SlowConfig;
}
