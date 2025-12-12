import { PushoverPriority } from './pushover-priority.enum';

export interface PushoverConfig {
  id?: string;
  notificationConfigId?: string;
  apiToken?: string;
  userKey?: string;
  devices?: string[];
  priority?: PushoverPriority;
  sound?: string;
  retry?: number;
  expire?: number;
  tags?: string[];
}
