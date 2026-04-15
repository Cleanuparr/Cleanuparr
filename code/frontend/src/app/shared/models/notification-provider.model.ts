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
  onSearchTriggered: boolean;
  onSearchItemGrabbed: boolean;
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

interface CreateProviderRequestBase extends NotificationEventFlags {
  name: string;
  isEnabled: boolean;
}

export interface CreateNotifiarrProviderRequest extends CreateProviderRequestBase {
  apiKey: string;
  channelId: string;
}

export interface CreateAppriseProviderRequest extends CreateProviderRequestBase {
  mode: AppriseMode;
  url?: string;
  key?: string;
  tags?: string;
  serviceUrls?: string;
}

export interface CreateNtfyProviderRequest extends CreateProviderRequestBase {
  serverUrl: string;
  topics: string[];
  authenticationType: NtfyAuthenticationType;
  username?: string;
  password?: string;
  accessToken?: string;
  priority: NtfyPriority;
  tags?: string[];
}

export interface CreateTelegramProviderRequest extends CreateProviderRequestBase {
  botToken: string;
  chatId: string;
  topicId?: string;
  sendSilently: boolean;
}

export interface CreateDiscordProviderRequest extends CreateProviderRequestBase {
  webhookUrl: string;
  username?: string;
  avatarUrl?: string;
}

export interface CreatePushoverProviderRequest extends CreateProviderRequestBase {
  apiToken: string;
  userKey: string;
  devices?: string[];
  priority: PushoverPriority;
  sound?: string;
  retry?: number;
  expire?: number;
  tags?: string[];
}

export interface CreateGotifyProviderRequest extends CreateProviderRequestBase {
  serverUrl: string;
  applicationToken: string;
  priority: number;
}

// Test request types (minimal, no event flags needed)

export interface TestNotifiarrRequest {
  apiKey: string;
  channelId: string;
  providerId?: string;
}

export interface TestAppriseRequest {
  mode: AppriseMode;
  url?: string;
  key?: string;
  tags?: string;
  serviceUrls?: string;
  providerId?: string;
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
  providerId?: string;
}

export interface TestTelegramRequest {
  botToken: string;
  chatId: string;
  topicId?: string;
  sendSilently: boolean;
  providerId?: string;
}

export interface TestDiscordRequest {
  webhookUrl: string;
  username?: string;
  avatarUrl?: string;
  providerId?: string;
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
  providerId?: string;
}

export interface TestGotifyRequest {
  serverUrl: string;
  applicationToken: string;
  priority: number;
  providerId?: string;
}

export interface TestNotificationResult {
  message: string;
}
