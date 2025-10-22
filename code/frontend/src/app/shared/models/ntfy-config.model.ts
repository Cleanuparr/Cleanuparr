import { NotificationConfig } from './notification-config.model';
import { NtfyAuthenticationType } from './ntfy-authentication-type.enum';
import { NtfyPriority } from './ntfy-priority.enum';

export interface NtfyConfig extends NotificationConfig {
  serverUrl?: string;
  topics?: string[];
  authenticationType?: NtfyAuthenticationType;
  username?: string;
  password?: string;
  accessToken?: string;
  priority?: NtfyPriority;
  tags?: string[];
}
