import { NotificationConfig } from './notification-config.model';

export interface AppriseConfig extends NotificationConfig {
  fullUrl?: string;
  key?: string;
  tags?: string;
} 
