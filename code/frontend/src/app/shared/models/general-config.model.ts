import { CertificateValidationType, LogEventLevel } from './enums';

export interface LoggingConfig {
  level: LogEventLevel;
  rollingSizeMB: number;
  retainedFileCount: number;
  timeLimitHours: number;
  archiveEnabled: boolean;
  archiveRetainedCount: number;
  archiveTimeLimitHours: number;
}

export interface GeneralConfig {
  displaySupportBanner: boolean;
  dryRun: boolean;
  httpMaxRetries: number;
  httpTimeout: number;
  httpCertificateValidation: CertificateValidationType;
  searchEnabled: boolean;
  searchDelay: number;
  log?: LoggingConfig;
  ignoredDownloads: string[];
}
