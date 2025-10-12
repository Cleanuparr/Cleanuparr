import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApplicationPathService } from './base-path.service';
import { 
  NotificationProvidersConfig, 
  NotificationProviderDto, 
  TestNotificationResult
} from '../../shared/models/notification-provider.model';
import { NotificationProviderType } from '../../shared/models/enums';
import { NtfyAuthenticationType } from '../../shared/models/ntfy-authentication-type.enum';
import { NtfyPriority } from '../../shared/models/ntfy-priority.enum';

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
  url: string;
  key: string;
  tags: string;
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
  url: string;
  key: string;
  tags: string;
}

export interface TestAppriseProviderRequest {
  url: string;
  key: string;
  tags: string;
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
      default:
        throw new Error(`Unsupported provider type: ${type}`);
    }
  }
}
