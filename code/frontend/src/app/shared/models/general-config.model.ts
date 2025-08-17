import { CertificateValidationType } from './certificate-validation-type.enum';
import { LoggingConfig } from './logging-config.model';
import { LogEventLevel } from './log-event-level.enum';

export interface GeneralConfig {
  displaySupportBanner: boolean;
  dryRun: boolean;
  httpMaxRetries: number;
  httpTimeout: number;
  httpCertificateValidation: CertificateValidationType;
  searchEnabled: boolean;
  searchDelay: number;
  // New logging configuration structure
  log?: LoggingConfig;
  // Temporary backward compatibility - will be removed in task 7
  logLevel?: LogEventLevel;
  ignoredDownloads: string[];
}
