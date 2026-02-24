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

export interface OidcConfig {
  enabled: boolean;
  issuerUrl: string;
  clientId: string;
  clientSecret: string;
  scopes: string;
  authorizedSubject: string;
  providerName: string;
}

export interface AuthConfig {
  disableAuthForLocalAddresses: boolean;
  trustForwardedHeaders: boolean;
  trustedNetworks: string[];
  oidc?: OidcConfig;
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
