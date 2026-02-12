import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  NotificationProvidersConfig,
  NotificationProviderDto,
  AppriseCliStatus,
  TestNotificationResult,
  CreateNotifiarrProviderRequest,
  CreateAppriseProviderRequest,
  CreateNtfyProviderRequest,
  CreateTelegramProviderRequest,
  CreateDiscordProviderRequest,
  CreatePushoverProviderRequest,
  CreateGotifyProviderRequest,
  TestNotifiarrRequest,
  TestAppriseRequest,
  TestNtfyRequest,
  TestTelegramRequest,
  TestDiscordRequest,
  TestPushoverRequest,
  TestGotifyRequest,
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

  // Create providers
  createNotifiarr(data: CreateNotifiarrProviderRequest): Observable<NotificationProviderDto> {
    return this.http.post<NotificationProviderDto>(`${BASE}/notifiarr`, data);
  }

  createApprise(data: CreateAppriseProviderRequest): Observable<NotificationProviderDto> {
    return this.http.post<NotificationProviderDto>(`${BASE}/apprise`, data);
  }

  createNtfy(data: CreateNtfyProviderRequest): Observable<NotificationProviderDto> {
    return this.http.post<NotificationProviderDto>(`${BASE}/ntfy`, data);
  }

  createTelegram(data: CreateTelegramProviderRequest): Observable<NotificationProviderDto> {
    return this.http.post<NotificationProviderDto>(`${BASE}/telegram`, data);
  }

  createDiscord(data: CreateDiscordProviderRequest): Observable<NotificationProviderDto> {
    return this.http.post<NotificationProviderDto>(`${BASE}/discord`, data);
  }

  createPushover(data: CreatePushoverProviderRequest): Observable<NotificationProviderDto> {
    return this.http.post<NotificationProviderDto>(`${BASE}/pushover`, data);
  }

  createGotify(data: CreateGotifyProviderRequest): Observable<NotificationProviderDto> {
    return this.http.post<NotificationProviderDto>(`${BASE}/gotify`, data);
  }

  // Update providers (same request types, with id in URL)
  updateNotifiarr(id: string, data: CreateNotifiarrProviderRequest): Observable<NotificationProviderDto> {
    return this.http.put<NotificationProviderDto>(`${BASE}/notifiarr/${id}`, data);
  }

  updateApprise(id: string, data: CreateAppriseProviderRequest): Observable<NotificationProviderDto> {
    return this.http.put<NotificationProviderDto>(`${BASE}/apprise/${id}`, data);
  }

  updateNtfy(id: string, data: CreateNtfyProviderRequest): Observable<NotificationProviderDto> {
    return this.http.put<NotificationProviderDto>(`${BASE}/ntfy/${id}`, data);
  }

  updateTelegram(id: string, data: CreateTelegramProviderRequest): Observable<NotificationProviderDto> {
    return this.http.put<NotificationProviderDto>(`${BASE}/telegram/${id}`, data);
  }

  updateDiscord(id: string, data: CreateDiscordProviderRequest): Observable<NotificationProviderDto> {
    return this.http.put<NotificationProviderDto>(`${BASE}/discord/${id}`, data);
  }

  updatePushover(id: string, data: CreatePushoverProviderRequest): Observable<NotificationProviderDto> {
    return this.http.put<NotificationProviderDto>(`${BASE}/pushover/${id}`, data);
  }

  updateGotify(id: string, data: CreateGotifyProviderRequest): Observable<NotificationProviderDto> {
    return this.http.put<NotificationProviderDto>(`${BASE}/gotify/${id}`, data);
  }

  // Test providers
  testNotifiarr(data: TestNotifiarrRequest): Observable<TestNotificationResult> {
    return this.http.post<TestNotificationResult>(`${BASE}/notifiarr/test`, data);
  }

  testApprise(data: TestAppriseRequest): Observable<TestNotificationResult> {
    return this.http.post<TestNotificationResult>(`${BASE}/apprise/test`, data);
  }

  testNtfy(data: TestNtfyRequest): Observable<TestNotificationResult> {
    return this.http.post<TestNotificationResult>(`${BASE}/ntfy/test`, data);
  }

  testTelegram(data: TestTelegramRequest): Observable<TestNotificationResult> {
    return this.http.post<TestNotificationResult>(`${BASE}/telegram/test`, data);
  }

  testDiscord(data: TestDiscordRequest): Observable<TestNotificationResult> {
    return this.http.post<TestNotificationResult>(`${BASE}/discord/test`, data);
  }

  testPushover(data: TestPushoverRequest): Observable<TestNotificationResult> {
    return this.http.post<TestNotificationResult>(`${BASE}/pushover/test`, data);
  }

  testGotify(data: TestGotifyRequest): Observable<TestNotificationResult> {
    return this.http.post<TestNotificationResult>(`${BASE}/gotify/test`, data);
  }
}
