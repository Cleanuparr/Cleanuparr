import { NotificationConfig } from './notification-config.model';
import { AppriseMode } from './enums';

export interface AppriseConfig extends NotificationConfig {
  mode: AppriseMode;
  // API mode fields
  url?: string;
  key?: string;
  tags?: string;
  // CLI mode fields
  serviceUrls?: string;
} 
