import { CertificateValidationType } from './certificate-validation-type.enum';
import { LoggingConfig } from './logging-config.model';

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
