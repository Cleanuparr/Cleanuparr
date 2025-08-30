import { NotificationProviderType } from './enums';

export interface NotificationEventFlags {
  onFailedImportStrike: boolean;
  onStalledStrike: boolean;
  onSlowStrike: boolean;
  onQueueItemDeleted: boolean;
  onDownloadCleaned: boolean;
  onCategoryChanged: boolean;
}

export interface NotificationProviderDto {
  id: string;
  name: string;
  type: NotificationProviderType;
  isEnabled: boolean;
  events: NotificationEventFlags;
  configuration: any;
}

export interface CreateNotificationProviderDto {
  name: string;
  type: NotificationProviderType;
  isEnabled: boolean;
  onFailedImportStrike: boolean;
  onStalledStrike: boolean;
  onSlowStrike: boolean;
  onQueueItemDeleted: boolean;
  onDownloadCleaned: boolean;
  onCategoryChanged: boolean;
  configuration: any;
}

export interface UpdateNotificationProviderDto {
  name: string;
  type: NotificationProviderType;
  isEnabled: boolean;
  onFailedImportStrike: boolean;
  onStalledStrike: boolean;
  onSlowStrike: boolean;
  onQueueItemDeleted: boolean;
  onDownloadCleaned: boolean;
  onCategoryChanged: boolean;
  configuration: any;
}

export interface NotificationProvidersConfig {
  providers: NotificationProviderDto[];
}

// Provider-specific configuration interfaces
export interface NotifiarrConfiguration {
  apiKey: string;
  channelId: string;
}

export interface AppriseConfiguration {
  url: string;
  key: string;
  tags: string;
}

export interface TestNotificationResult {
  success: boolean;
  message: string;
  error?: string;
}
