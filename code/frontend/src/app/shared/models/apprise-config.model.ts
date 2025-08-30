import { NotificationConfig } from './notification-config.model';

export interface AppriseConfig extends NotificationConfig {
  url?: string;
  key?: string;
  tags?: string;
} 
