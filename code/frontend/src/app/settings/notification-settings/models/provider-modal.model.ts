import { NotificationProviderType } from '../../../shared/models/enums';
import { NtfyAuthenticationType } from '../../../shared/models/ntfy-authentication-type.enum';
import { NtfyPriority } from '../../../shared/models/ntfy-priority.enum';
import { PushoverPriority } from '../../../shared/models/pushover-priority.enum';

export interface ProviderTypeInfo {
  type: NotificationProviderType;
  name: string;
  iconUrl: string;
  iconUrlHover?: string;
  description?: string;
}

export interface ProviderModalConfig {
  visible: boolean;
  mode: 'add' | 'edit';
  providerId?: string;
}

export interface BaseProviderFormData {
  name: string;
  enabled: boolean;
  onFailedImportStrike: boolean;
  onStalledStrike: boolean;
  onSlowStrike: boolean;
  onQueueItemDeleted: boolean;
  onDownloadCleaned: boolean;
  onCategoryChanged: boolean;
}

export interface NotifiarrFormData extends BaseProviderFormData {
  apiKey: string;
  channelId: string;
}

export interface AppriseFormData extends BaseProviderFormData {
  url: string;
  key: string;
  tags: string;
}

export interface NtfyFormData extends BaseProviderFormData {
  serverUrl: string;
  topics: string[];
  authenticationType: NtfyAuthenticationType;
  username: string;
  password: string;
  accessToken: string;
  priority: NtfyPriority;
  tags: string[];
}

export interface PushoverFormData extends BaseProviderFormData {
  apiToken: string;
  userKey: string;
  devices: string[];
  priority: PushoverPriority;
  sound: string;
  retry: number | null;
  expire: number | null;
  tags: string[];
}

// Events for modal communication
export interface ProviderModalEvents {
  save: (data: any) => void;
  cancel: () => void;
  test: (data: any) => void;
}
