import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApplicationPathService } from './base-path.service';
import { 
  NotificationProvidersConfig, 
  NotificationProviderDto, 
  CreateNotificationProviderDto, 
  UpdateNotificationProviderDto,
  TestNotificationResult
} from '../../shared/models/notification-provider.model';

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
   * Create a new notification provider
   */
  createProvider(provider: CreateNotificationProviderDto): Observable<NotificationProviderDto> {
    return this.http.post<NotificationProviderDto>(`${this.baseUrl}/notification_providers`, provider);
  }

  /**
   * Update an existing notification provider
   */
  updateProvider(id: string, provider: UpdateNotificationProviderDto): Observable<NotificationProviderDto> {
    return this.http.put<NotificationProviderDto>(`${this.baseUrl}/notification_providers/${id}`, provider);
  }

  /**
   * Delete a notification provider
   */
  deleteProvider(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/notification_providers/${id}`);
  }

  /**
   * Test a notification provider
   */
  testProvider(id: string): Observable<TestNotificationResult> {
    return this.http.post<TestNotificationResult>(`${this.baseUrl}/notification_providers/${id}/test`, {});
  }
}
