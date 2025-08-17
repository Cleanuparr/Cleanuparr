import { LogEventLevel } from './log-event-level.enum';

/**
 * Interface representing logging configuration options
 */
export interface LoggingConfig {
  level: LogEventLevel;
  rollingSizeMB: number; // 0 = disabled
  retainedFileCount: number; // 0 = unlimited
  timeLimitHours: number; // 0 = unlimited
  archiveEnabled: boolean;
  archiveRetainedCount: number; // 0 = unlimited
  archiveTimeLimitHours: number; // 0 = unlimited
}