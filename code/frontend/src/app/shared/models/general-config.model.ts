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

export interface AuthConfig {
  disableAuthForLocalAddresses: boolean;
  trustForwardedHeaders: boolean;
  trustedNetworks: string[];
}

export interface GeneralConfig {
  displaySupportBanner: boolean;
  dryRun: boolean;
  httpMaxRetries: number;
  httpTimeout: number;
  httpCertificateValidation: CertificateValidationType;
  searchEnabled: boolean;
  searchDelay: number;
  statusCheckEnabled: boolean;
  strikeInactivityWindowHours: number;
  log?: LoggingConfig;
  auth?: AuthConfig;
  ignoredDownloads: string[];
}
