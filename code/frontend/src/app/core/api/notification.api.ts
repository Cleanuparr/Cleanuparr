import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  NotificationProvidersConfig,
  NotificationProviderDto,
  AppriseCliStatus,
  TestNotificationResult,
} from '@shared/models/notification-provider.model';

const BASE = '/api/configuration/notification_providers';

@Injectable({ providedIn: 'root' })
export class NotificationApi {
  private http = inject(HttpClient);

  getProviders(): Observable<NotificationProvidersConfig> {
    return this.http.get<NotificationProvidersConfig>(BASE);
  }

  getAppriseCliStatus(): Observable<AppriseCliStatus> {
    return this.http.get<AppriseCliStatus>(`${BASE}/apprise/cli-status`);
  }

  deleteProvider(id: string): Observable<void> {
    return this.http.delete<void>(`${BASE}/${id}`);
  }

  create<TRequest>(urlSegment: string, data: TRequest): Observable<NotificationProviderDto> {
    return this.http.post<NotificationProviderDto>(`${BASE}/${urlSegment}`, data);
  }

  update<TRequest>(urlSegment: string, id: string, data: TRequest): Observable<NotificationProviderDto> {
    return this.http.put<NotificationProviderDto>(`${BASE}/${urlSegment}/${id}`, data);
  }

  test<TRequest>(urlSegment: string, data: TRequest): Observable<TestNotificationResult> {
    return this.http.post<TestNotificationResult>(`${BASE}/${urlSegment}/test`, data);
  }
}
