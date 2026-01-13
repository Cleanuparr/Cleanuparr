import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApplicationPathService } from './base-path.service';
import {
  NotificationProvidersConfig,
  NotificationProviderDto,
  TestNotificationResult
} from '../../shared/models/notification-provider.model';
import { AppriseMode, NotificationProviderType } from '../../shared/models/enums';
import { NtfyAuthenticationType } from '../../shared/models/ntfy-authentication-type.enum';
import { NtfyPriority } from '../../shared/models/ntfy-priority.enum';
import { PushoverPriority } from '../../shared/models/pushover-priority.enum';

export interface AppriseCliStatus {
  available: boolean;
  version: string | null;
}

// Provider-specific interfaces
export interface CreateNotifiarrProviderRequest {
  name: string;
  isEnabled: boolean;
  onFailedImportStrike: boolean;
  onStalledStrike: boolean;
  onSlowStrike: boolean;
  onQueueItemDeleted: boolean;
  onDownloadCleaned: boolean;
  onCategoryChanged: boolean;
  apiKey: string;
  channelId: string;
}

export interface UpdateNotifiarrProviderRequest {
  name: string;
  isEnabled: boolean;
  onFailedImportStrike: boolean;
  onStalledStrike: boolean;
  onSlowStrike: boolean;
  onQueueItemDeleted: boolean;
  onDownloadCleaned: boolean;
  onCategoryChanged: boolean;
  apiKey: string;
  channelId: string;
}

export interface TestNotifiarrProviderRequest {
  apiKey: string;
  channelId: string;
}

export interface CreateAppriseProviderRequest {
  name: string;
  isEnabled: boolean;
  onFailedImportStrike: boolean;
  onStalledStrike: boolean;
  onSlowStrike: boolean;
  onQueueItemDeleted: boolean;
  onDownloadCleaned: boolean;
  onCategoryChanged: boolean;
  mode: AppriseMode;
  // API mode fields
  url: string;
  key: string;
  tags: string;
  // CLI mode fields
  serviceUrls: string;
}

export interface UpdateAppriseProviderRequest {
  name: string;
  isEnabled: boolean;
  onFailedImportStrike: boolean;
  onStalledStrike: boolean;
  onSlowStrike: boolean;
  onQueueItemDeleted: boolean;
  onDownloadCleaned: boolean;
  onCategoryChanged: boolean;
  mode: AppriseMode;
  // API mode fields
  url: string;
  key: string;
  tags: string;
  // CLI mode fields
  serviceUrls: string;
}

export interface TestAppriseProviderRequest {
  mode: AppriseMode;
  // API mode fields
  url: string;
  key: string;
  tags: string;
  // CLI mode fields
  serviceUrls: string;
}

export interface CreateNtfyProviderRequest {
  name: string;
  isEnabled: boolean;
  onFailedImportStrike: boolean;
  onStalledStrike: boolean;
  onSlowStrike: boolean;
  onQueueItemDeleted: boolean;
  onDownloadCleaned: boolean;
  onCategoryChanged: boolean;
  serverUrl: string;
  topics: string[];
  authenticationType: NtfyAuthenticationType;
  username: string;
  password: string;
  accessToken: string;
  priority: NtfyPriority;
  tags: string[];
}

export interface UpdateNtfyProviderRequest {
  name: string;
  isEnabled: boolean;
  onFailedImportStrike: boolean;
  onStalledStrike: boolean;
  onSlowStrike: boolean;
  onQueueItemDeleted: boolean;
  onDownloadCleaned: boolean;
  onCategoryChanged: boolean;
  serverUrl: string;
  topics: string[];
  authenticationType: NtfyAuthenticationType;
  username: string;
  password: string;
  accessToken: string;
  priority: NtfyPriority;
  tags: string[];
}

export interface TestNtfyProviderRequest {
  serverUrl: string;
  topics: string[];
  authenticationType: NtfyAuthenticationType;
  username: string;
  password: string;
  accessToken: string;
  priority: NtfyPriority;
  tags: string[];
}

export interface CreatePushoverProviderRequest {
  name: string;
  isEnabled: boolean;
  onFailedImportStrike: boolean;
  onStalledStrike: boolean;
  onSlowStrike: boolean;
  onQueueItemDeleted: boolean;
  onDownloadCleaned: boolean;
  onCategoryChanged: boolean;
  apiToken: string;
  userKey: string;
  devices: string[];
  priority: PushoverPriority;
  sound: string | null;
  retry: number | null;
  expire: number | null;
  tags: string[];
}

export interface UpdatePushoverProviderRequest {
  name: string;
  isEnabled: boolean;
  onFailedImportStrike: boolean;
  onStalledStrike: boolean;
  onSlowStrike: boolean;
  onQueueItemDeleted: boolean;
  onDownloadCleaned: boolean;
  onCategoryChanged: boolean;
  apiToken: string;
  userKey: string;
  devices: string[];
  priority: PushoverPriority;
  sound: string | null;
  retry: number | null;
  expire: number | null;
  tags: string[];
}

export interface TestPushoverProviderRequest {
  apiToken: string;
  userKey: string;
  devices: string[];
  priority: PushoverPriority;
  sound: string | null;
  retry: number | null;
  expire: number | null;
  tags: string[];
}

export interface CreateTelegramProviderRequest {
  name: string;
  isEnabled: boolean;
  onFailedImportStrike: boolean;
  onStalledStrike: boolean;
  onSlowStrike: boolean;
  onQueueItemDeleted: boolean;
  onDownloadCleaned: boolean;
  onCategoryChanged: boolean;
  botToken: string;
  chatId: string;
  topicId: string;
  sendSilently: boolean;
}

export interface UpdateTelegramProviderRequest {
  name: string;
  isEnabled: boolean;
  onFailedImportStrike: boolean;
  onStalledStrike: boolean;
  onSlowStrike: boolean;
  onQueueItemDeleted: boolean;
  onDownloadCleaned: boolean;
  onCategoryChanged: boolean;
  botToken: string;
  chatId: string;
  topicId: string;
  sendSilently: boolean;
}

export interface TestTelegramProviderRequest {
  botToken: string;
  chatId: string;
  topicId: string;
  sendSilently: boolean;
}

export interface CreateDiscordProviderRequest {
  name: string;
  isEnabled: boolean;
  onFailedImportStrike: boolean;
  onStalledStrike: boolean;
  onSlowStrike: boolean;
  onQueueItemDeleted: boolean;
  onDownloadCleaned: boolean;
  onCategoryChanged: boolean;
  webhookUrl: string;
  username: string;
  avatarUrl: string;
}

export interface UpdateDiscordProviderRequest {
  name: string;
  isEnabled: boolean;
  onFailedImportStrike: boolean;
  onStalledStrike: boolean;
  onSlowStrike: boolean;
  onQueueItemDeleted: boolean;
  onDownloadCleaned: boolean;
  onCategoryChanged: boolean;
  webhookUrl: string;
  username: string;
  avatarUrl: string;
}

export interface TestDiscordProviderRequest {
  webhookUrl: string;
  username: string;
  avatarUrl: string;
}

export interface CreateGotifyProviderRequest {
  name: string;
  isEnabled: boolean;
  onFailedImportStrike: boolean;
  onStalledStrike: boolean;
  onSlowStrike: boolean;
  onQueueItemDeleted: boolean;
  onDownloadCleaned: boolean;
  onCategoryChanged: boolean;
  serverUrl: string;
  applicationToken: string;
  priority: number;
}

export interface UpdateGotifyProviderRequest {
  name: string;
  isEnabled: boolean;
  onFailedImportStrike: boolean;
  onStalledStrike: boolean;
  onSlowStrike: boolean;
  onQueueItemDeleted: boolean;
  onDownloadCleaned: boolean;
  onCategoryChanged: boolean;
  serverUrl: string;
  applicationToken: string;
  priority: number;
}

export interface TestGotifyProviderRequest {
  serverUrl: string;
  applicationToken: string;
  priority: number;
}

@Injectable({
  providedIn: 'root'
})
export class NotificationProviderService {
  private readonly http = inject(HttpClient);
  private readonly pathService = inject(ApplicationPathService);
  private readonly baseUrl = this.pathService.buildApiUrl('/configuration/notification_providers');

  /**
   * Get all notification providers
   */
  getProviders(): Observable<NotificationProvidersConfig> {
    return this.http.get<NotificationProvidersConfig>(this.baseUrl);
  }

  /**
   * Get Apprise CLI availability status
   */
  getAppriseCliStatus(): Observable<AppriseCliStatus> {
    return this.http.get<AppriseCliStatus>(`${this.baseUrl}/apprise/cli-status`);
  }

  /**
   * Create a new Notifiarr provider
   */
  createNotifiarrProvider(provider: CreateNotifiarrProviderRequest): Observable<NotificationProviderDto> {
    return this.http.post<NotificationProviderDto>(`${this.baseUrl}/notifiarr`, provider);
  }

  /**
   * Create a new Apprise provider
   */
  createAppriseProvider(provider: CreateAppriseProviderRequest): Observable<NotificationProviderDto> {
    return this.http.post<NotificationProviderDto>(`${this.baseUrl}/apprise`, provider);
  }

  /**
   * Create a new Ntfy provider
   */
  createNtfyProvider(provider: CreateNtfyProviderRequest): Observable<NotificationProviderDto> {
    return this.http.post<NotificationProviderDto>(`${this.baseUrl}/ntfy`, provider);
  }

  /**
   * Create a new Pushover provider
   */
  createPushoverProvider(provider: CreatePushoverProviderRequest): Observable<NotificationProviderDto> {
    return this.http.post<NotificationProviderDto>(`${this.baseUrl}/pushover`, provider);
  }

  /**
   * Create a new Telegram provider
   */
  createTelegramProvider(provider: CreateTelegramProviderRequest): Observable<NotificationProviderDto> {
    return this.http.post<NotificationProviderDto>(`${this.baseUrl}/telegram`, provider);
  }

  /**
   * Create a new Discord provider
   */
  createDiscordProvider(provider: CreateDiscordProviderRequest): Observable<NotificationProviderDto> {
    return this.http.post<NotificationProviderDto>(`${this.baseUrl}/discord`, provider);
  }

  /**
   * Create a new Gotify provider
   */
  createGotifyProvider(provider: CreateGotifyProviderRequest): Observable<NotificationProviderDto> {
    return this.http.post<NotificationProviderDto>(`${this.baseUrl}/gotify`, provider);
  }

  /**
   * Update an existing Notifiarr provider
   */
  updateNotifiarrProvider(id: string, provider: UpdateNotifiarrProviderRequest): Observable<NotificationProviderDto> {
    return this.http.put<NotificationProviderDto>(`${this.baseUrl}/notifiarr/${id}`, provider);
  }

  /**
   * Update an existing Apprise provider
   */
  updateAppriseProvider(id: string, provider: UpdateAppriseProviderRequest): Observable<NotificationProviderDto> {
    return this.http.put<NotificationProviderDto>(`${this.baseUrl}/apprise/${id}`, provider);
  }

  /**
   * Update an existing Ntfy provider
   */
  updateNtfyProvider(id: string, provider: UpdateNtfyProviderRequest): Observable<NotificationProviderDto> {
    return this.http.put<NotificationProviderDto>(`${this.baseUrl}/ntfy/${id}`, provider);
  }

  /**
   * Update an existing Pushover provider
   */
  updatePushoverProvider(id: string, provider: UpdatePushoverProviderRequest): Observable<NotificationProviderDto> {
    return this.http.put<NotificationProviderDto>(`${this.baseUrl}/pushover/${id}`, provider);
  }

  /**
   * Update an existing Telegram provider
   */
  updateTelegramProvider(id: string, provider: UpdateTelegramProviderRequest): Observable<NotificationProviderDto> {
    return this.http.put<NotificationProviderDto>(`${this.baseUrl}/telegram/${id}`, provider);
  }

  /**
   * Update an existing Discord provider
   */
  updateDiscordProvider(id: string, provider: UpdateDiscordProviderRequest): Observable<NotificationProviderDto> {
    return this.http.put<NotificationProviderDto>(`${this.baseUrl}/discord/${id}`, provider);
  }

  /**
   * Update an existing Gotify provider
   */
  updateGotifyProvider(id: string, provider: UpdateGotifyProviderRequest): Observable<NotificationProviderDto> {
    return this.http.put<NotificationProviderDto>(`${this.baseUrl}/gotify/${id}`, provider);
  }

  /**
   * Delete a notification provider
   */
  deleteProvider(id: string): Observable<void> {
  return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  /**
   * Test a Notifiarr provider (without ID - for testing configuration before saving)
   */
  testNotifiarrProvider(testRequest: TestNotifiarrProviderRequest): Observable<TestNotificationResult> {
    return this.http.post<TestNotificationResult>(`${this.baseUrl}/notifiarr/test`, testRequest);
  }

  /**
   * Test an Apprise provider (without ID - for testing configuration before saving)
   */
  testAppriseProvider(testRequest: TestAppriseProviderRequest): Observable<TestNotificationResult> {
    return this.http.post<TestNotificationResult>(`${this.baseUrl}/apprise/test`, testRequest);
  }

  /**
   * Test an Ntfy provider (without ID - for testing configuration before saving)
   */
  testNtfyProvider(testRequest: TestNtfyProviderRequest): Observable<TestNotificationResult> {
    return this.http.post<TestNotificationResult>(`${this.baseUrl}/ntfy/test`, testRequest);
  }

  /**
   * Test a Pushover provider (without ID - for testing configuration before saving)
   */
  testPushoverProvider(testRequest: TestPushoverProviderRequest): Observable<TestNotificationResult> {
    return this.http.post<TestNotificationResult>(`${this.baseUrl}/pushover/test`, testRequest);
  }

  /**
   * Test a Telegram provider (without ID - for testing configuration before saving)
   */
  testTelegramProvider(testRequest: TestTelegramProviderRequest): Observable<TestNotificationResult> {
    return this.http.post<TestNotificationResult>(`${this.baseUrl}/telegram/test`, testRequest);
  }

  /**
   * Test a Discord provider (without ID - for testing configuration before saving)
   */
  testDiscordProvider(testRequest: TestDiscordProviderRequest): Observable<TestNotificationResult> {
    return this.http.post<TestNotificationResult>(`${this.baseUrl}/discord/test`, testRequest);
  }

  /**
   * Test a Gotify provider (without ID - for testing configuration before saving)
   */
  testGotifyProvider(testRequest: TestGotifyProviderRequest): Observable<TestNotificationResult> {
    return this.http.post<TestNotificationResult>(`${this.baseUrl}/gotify/test`, testRequest);
  }

  /**
   * Generic create method that delegates to provider-specific methods
   */
  createProvider(provider: any, type: NotificationProviderType): Observable<NotificationProviderDto> {
    switch (type) {
      case NotificationProviderType.Notifiarr:
        return this.createNotifiarrProvider(provider as CreateNotifiarrProviderRequest);
      case NotificationProviderType.Apprise:
        return this.createAppriseProvider(provider as CreateAppriseProviderRequest);
      case NotificationProviderType.Ntfy:
        return this.createNtfyProvider(provider as CreateNtfyProviderRequest);
      case NotificationProviderType.Pushover:
        return this.createPushoverProvider(provider as CreatePushoverProviderRequest);
      case NotificationProviderType.Telegram:
        return this.createTelegramProvider(provider as CreateTelegramProviderRequest);
      case NotificationProviderType.Discord:
        return this.createDiscordProvider(provider as CreateDiscordProviderRequest);
      case NotificationProviderType.Gotify:
        return this.createGotifyProvider(provider as CreateGotifyProviderRequest);
      default:
        throw new Error(`Unsupported provider type: ${type}`);
    }
  }

  /**
   * Generic update method that delegates to provider-specific methods
   */
  updateProvider(id: string, provider: any, type: NotificationProviderType): Observable<NotificationProviderDto> {
    switch (type) {
      case NotificationProviderType.Notifiarr:
        return this.updateNotifiarrProvider(id, provider as UpdateNotifiarrProviderRequest);
      case NotificationProviderType.Apprise:
        return this.updateAppriseProvider(id, provider as UpdateAppriseProviderRequest);
      case NotificationProviderType.Ntfy:
        return this.updateNtfyProvider(id, provider as UpdateNtfyProviderRequest);
      case NotificationProviderType.Pushover:
        return this.updatePushoverProvider(id, provider as UpdatePushoverProviderRequest);
      case NotificationProviderType.Telegram:
        return this.updateTelegramProvider(id, provider as UpdateTelegramProviderRequest);
      case NotificationProviderType.Discord:
        return this.updateDiscordProvider(id, provider as UpdateDiscordProviderRequest);
      case NotificationProviderType.Gotify:
        return this.updateGotifyProvider(id, provider as UpdateGotifyProviderRequest);
      default:
        throw new Error(`Unsupported provider type: ${type}`);
    }
  }

  /**
   * Generic test method that delegates to provider-specific methods
   */
  testProvider(testRequest: any, type: NotificationProviderType): Observable<TestNotificationResult> {
    switch (type) {
      case NotificationProviderType.Notifiarr:
        return this.testNotifiarrProvider(testRequest as TestNotifiarrProviderRequest);
      case NotificationProviderType.Apprise:
        return this.testAppriseProvider(testRequest as TestAppriseProviderRequest);
      case NotificationProviderType.Ntfy:
        return this.testNtfyProvider(testRequest as TestNtfyProviderRequest);
      case NotificationProviderType.Pushover:
        return this.testPushoverProvider(testRequest as TestPushoverProviderRequest);
      case NotificationProviderType.Telegram:
        return this.testTelegramProvider(testRequest as TestTelegramProviderRequest);
      case NotificationProviderType.Discord:
        return this.testDiscordProvider(testRequest as TestDiscordProviderRequest);
      case NotificationProviderType.Gotify:
        return this.testGotifyProvider(testRequest as TestGotifyProviderRequest);
      default:
        throw new Error(`Unsupported provider type: ${type}`);
    }
  }
}
