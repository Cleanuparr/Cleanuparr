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
export interface CreateNotifiarrProviderDto {
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

export interface UpdateNotifiarrProviderDto {
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

export interface TestNotifiarrProviderDto {
  apiKey: string;
  channelId: string;
}

export interface CreateAppriseProviderDto {
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

export interface UpdateAppriseProviderDto {
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

export interface TestAppriseProviderDto {
  url: string;
  key: string;
  tags: string;
}

export interface CreateNtfyProviderDto {
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

export interface UpdateNtfyProviderDto {
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

export interface TestNtfyProviderDto {
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
  private readonly baseUrl = this.pathService.buildApiUrl('/configuration');

  /**
   * Get all notification providers
   */
  getProviders(): Observable<NotificationProvidersConfig> {
    return this.http.get<NotificationProvidersConfig>(`${this.baseUrl}/notification_providers`);
  }

  /**
   * Create a new Notifiarr provider
   */
  createNotifiarrProvider(provider: CreateNotifiarrProviderDto): Observable<NotificationProviderDto> {
    return this.http.post<NotificationProviderDto>(`${this.baseUrl}/notification_providers/notifiarr`, provider);
  }

  /**
   * Create a new Apprise provider
   */
  createAppriseProvider(provider: CreateAppriseProviderDto): Observable<NotificationProviderDto> {
    return this.http.post<NotificationProviderDto>(`${this.baseUrl}/notification_providers/apprise`, provider);
  }

  /**
   * Create a new Ntfy provider
   */
  createNtfyProvider(provider: CreateNtfyProviderDto): Observable<NotificationProviderDto> {
    return this.http.post<NotificationProviderDto>(`${this.baseUrl}/notification_providers/ntfy`, provider);
  }

  /**
   * Update an existing Notifiarr provider
   */
  updateNotifiarrProvider(id: string, provider: UpdateNotifiarrProviderDto): Observable<NotificationProviderDto> {
    return this.http.put<NotificationProviderDto>(`${this.baseUrl}/notification_providers/notifiarr/${id}`, provider);
  }

  /**
   * Update an existing Apprise provider
   */
  updateAppriseProvider(id: string, provider: UpdateAppriseProviderDto): Observable<NotificationProviderDto> {
    return this.http.put<NotificationProviderDto>(`${this.baseUrl}/notification_providers/apprise/${id}`, provider);
  }

  /**
   * Update an existing Ntfy provider
   */
  updateNtfyProvider(id: string, provider: UpdateNtfyProviderDto): Observable<NotificationProviderDto> {
    return this.http.put<NotificationProviderDto>(`${this.baseUrl}/notification_providers/ntfy/${id}`, provider);
  }

  /**
   * Delete a notification provider
   */
  deleteProvider(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/notification_providers/${id}`);
  }

  /**
   * Test a Notifiarr provider (without ID - for testing configuration before saving)
   */
  testNotifiarrProvider(testRequest: TestNotifiarrProviderDto): Observable<TestNotificationResult> {
    return this.http.post<TestNotificationResult>(`${this.baseUrl}/notification_providers/notifiarr/test`, testRequest);
  }

  /**
   * Test an Apprise provider (without ID - for testing configuration before saving)
   */
  testAppriseProvider(testRequest: TestAppriseProviderDto): Observable<TestNotificationResult> {
    return this.http.post<TestNotificationResult>(`${this.baseUrl}/notification_providers/apprise/test`, testRequest);
  }

  /**
   * Test an Ntfy provider (without ID - for testing configuration before saving)
   */
  testNtfyProvider(testRequest: TestNtfyProviderDto): Observable<TestNotificationResult> {
    return this.http.post<TestNotificationResult>(`${this.baseUrl}/notification_providers/ntfy/test`, testRequest);
  }

  /**
   * Generic create method that delegates to provider-specific methods
   */
  createProvider(provider: any, type: NotificationProviderType): Observable<NotificationProviderDto> {
    switch (type) {
      case NotificationProviderType.Notifiarr:
        return this.createNotifiarrProvider(provider as CreateNotifiarrProviderDto);
      case NotificationProviderType.Apprise:
        return this.createAppriseProvider(provider as CreateAppriseProviderDto);
      case NotificationProviderType.Ntfy:
        return this.createNtfyProvider(provider as CreateNtfyProviderDto);
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
        return this.updateNotifiarrProvider(id, provider as UpdateNotifiarrProviderDto);
      case NotificationProviderType.Apprise:
        return this.updateAppriseProvider(id, provider as UpdateAppriseProviderDto);
      case NotificationProviderType.Ntfy:
        return this.updateNtfyProvider(id, provider as UpdateNtfyProviderDto);
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
        return this.testNotifiarrProvider(testRequest as TestNotifiarrProviderDto);
      case NotificationProviderType.Apprise:
        return this.testAppriseProvider(testRequest as TestAppriseProviderDto);
      case NotificationProviderType.Ntfy:
        return this.testNtfyProvider(testRequest as TestNtfyProviderDto);
      default:
        throw new Error(`Unsupported provider type: ${type}`);
    }
  }
}
