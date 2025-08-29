import { NotificationProviderType } from '../../../shared/models/enums';

export interface ProviderTypeInfo {
  type: NotificationProviderType;
  name: string;
  iconClass: string;
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
  fullUrl: string;
  key: string;
  tags: string;
}

// Events for modal communication
export interface ProviderModalEvents {
  save: (data: any) => void;
  cancel: () => void;
  test: (data: any) => void;
}
