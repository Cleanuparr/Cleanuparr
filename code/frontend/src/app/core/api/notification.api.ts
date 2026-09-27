import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  NotificationProvidersConfig,
  NotificationProviderDto,
  AppriseCliStatus,
  TestNotificationResult,
} from '@shared/models/notification-provider.model';
import { NotificationProviderDescriptor } from '@shared/utils/notification-provider.descriptors';

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

  create<TRequest>(descriptor: NotificationProviderDescriptor<TRequest, unknown>, data: TRequest): Observable<NotificationProviderDto> {
    return this.http.post<NotificationProviderDto>(`${BASE}/${descriptor.urlSegment}`, data);
  }

  update<TRequest>(descriptor: NotificationProviderDescriptor<TRequest, unknown>, id: string, data: TRequest): Observable<NotificationProviderDto> {
    return this.http.put<NotificationProviderDto>(`${BASE}/${descriptor.urlSegment}/${id}`, data);
  }

  test<TTestRequest>(descriptor: NotificationProviderDescriptor<unknown, TTestRequest>, data: TTestRequest): Observable<TestNotificationResult> {
    return this.http.post<TestNotificationResult>(`${BASE}/${descriptor.urlSegment}/test`, data);
  }
}
