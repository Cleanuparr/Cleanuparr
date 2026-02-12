import {
  NotificationProviderType,
  AppriseMode,
  NtfyAuthenticationType,
  NtfyPriority,
  PushoverPriority,
} from './enums';

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
  configuration: unknown;
}

export interface NotificationProvidersConfig {
  providers: NotificationProviderDto[];
}

export interface AppriseCliStatus {
  available: boolean;
  version?: string;
}

// Provider-specific create/update request types

export interface CreateNotifiarrProviderRequest {
  name: string;
  apiKey: string;
  channelId: string;
  isEnabled: boolean;
  onFailedImportStrike: boolean;
  onStalledStrike: boolean;
  onSlowStrike: boolean;
  onQueueItemDeleted: boolean;
  onDownloadCleaned: boolean;
  onCategoryChanged: boolean;
}

export interface CreateAppriseProviderRequest {
  name: string;
  mode: AppriseMode;
  url?: string;
  key?: string;
  tags?: string;
  serviceUrls?: string;
  isEnabled: boolean;
  onFailedImportStrike: boolean;
  onStalledStrike: boolean;
  onSlowStrike: boolean;
  onQueueItemDeleted: boolean;
  onDownloadCleaned: boolean;
  onCategoryChanged: boolean;
}

export interface CreateNtfyProviderRequest {
  name: string;
  serverUrl: string;
  topics: string[];
  authenticationType: NtfyAuthenticationType;
  username?: string;
  password?: string;
  accessToken?: string;
  priority: NtfyPriority;
  tags?: string[];
  isEnabled: boolean;
  onFailedImportStrike: boolean;
  onStalledStrike: boolean;
  onSlowStrike: boolean;
  onQueueItemDeleted: boolean;
  onDownloadCleaned: boolean;
  onCategoryChanged: boolean;
}

export interface CreateTelegramProviderRequest {
  name: string;
  botToken: string;
  chatId: string;
  topicId?: string;
  sendSilently: boolean;
  isEnabled: boolean;
  onFailedImportStrike: boolean;
  onStalledStrike: boolean;
  onSlowStrike: boolean;
  onQueueItemDeleted: boolean;
  onDownloadCleaned: boolean;
  onCategoryChanged: boolean;
}

export interface CreateDiscordProviderRequest {
  name: string;
  webhookUrl: string;
  username?: string;
  avatarUrl?: string;
  isEnabled: boolean;
  onFailedImportStrike: boolean;
  onStalledStrike: boolean;
  onSlowStrike: boolean;
  onQueueItemDeleted: boolean;
  onDownloadCleaned: boolean;
  onCategoryChanged: boolean;
}

export interface CreatePushoverProviderRequest {
  name: string;
  apiToken: string;
  userKey: string;
  devices?: string[];
  priority: PushoverPriority;
  sound?: string;
  retry?: number;
  expire?: number;
  tags?: string[];
  isEnabled: boolean;
  onFailedImportStrike: boolean;
  onStalledStrike: boolean;
  onSlowStrike: boolean;
  onQueueItemDeleted: boolean;
  onDownloadCleaned: boolean;
  onCategoryChanged: boolean;
}

export interface CreateGotifyProviderRequest {
  name: string;
  serverUrl: string;
  applicationToken: string;
  priority: number;
  isEnabled: boolean;
  onFailedImportStrike: boolean;
  onStalledStrike: boolean;
  onSlowStrike: boolean;
  onQueueItemDeleted: boolean;
  onDownloadCleaned: boolean;
  onCategoryChanged: boolean;
}

// Test request types (minimal, no event flags needed)

export interface TestNotifiarrRequest {
  apiKey: string;
  channelId: string;
}

export interface TestAppriseRequest {
  mode: AppriseMode;
  url?: string;
  key?: string;
  tags?: string;
  serviceUrls?: string;
}

export interface TestNtfyRequest {
  serverUrl: string;
  topics: string[];
  authenticationType: NtfyAuthenticationType;
  username?: string;
  password?: string;
  accessToken?: string;
  priority: NtfyPriority;
  tags?: string[];
}

export interface TestTelegramRequest {
  botToken: string;
  chatId: string;
  topicId?: string;
  sendSilently: boolean;
}

export interface TestDiscordRequest {
  webhookUrl: string;
  username?: string;
  avatarUrl?: string;
}

export interface TestPushoverRequest {
  apiToken: string;
  userKey: string;
  devices?: string[];
  priority: PushoverPriority;
  sound?: string;
  retry?: number;
  expire?: number;
  tags?: string[];
}

export interface TestGotifyRequest {
  serverUrl: string;
  applicationToken: string;
  priority: number;
}

export interface TestNotificationResult {
  message: string;
}
