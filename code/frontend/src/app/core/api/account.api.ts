import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface AccountInfo {
  username: string;
  plexLinked: boolean;
  plexUsername: string | null;
  twoFactorEnabled: boolean;
  apiKeyPreview: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface Regenerate2faRequest {
  password: string;
  totpCode: string;
}

export interface TotpSetupResponse {
  secret: string;
  qrCodeUri: string;
  recoveryCodes: string[];
}

export interface PlexPinResponse {
  pinId: number;
  authUrl: string;
}

export interface PlexPinStatus {
  completed: boolean;
  plexUsername?: string;
}

@Injectable({ providedIn: 'root' })
export class AccountApi {
  private http = inject(HttpClient);

  getInfo(): Observable<AccountInfo> {
    return this.http.get<AccountInfo>('/api/account');
  }

  changePassword(request: ChangePasswordRequest): Observable<void> {
    return this.http.put<void>('/api/account/password', request);
  }

  regenerate2fa(request: Regenerate2faRequest): Observable<TotpSetupResponse> {
    return this.http.post<TotpSetupResponse>('/api/account/2fa/regenerate', request);
  }

  getApiKey(): Observable<{ apiKey: string }> {
    return this.http.get<{ apiKey: string }>('/api/account/api-key');
  }

  regenerateApiKey(): Observable<{ apiKey: string }> {
    return this.http.post<{ apiKey: string }>('/api/account/api-key/regenerate', {});
  }

  linkPlex(): Observable<PlexPinResponse> {
    return this.http.post<PlexPinResponse>('/api/account/plex/link', {});
  }

  verifyPlexLink(pinId: number): Observable<PlexPinStatus> {
    return this.http.post<PlexPinStatus>('/api/account/plex/link/verify', { pinId });
  }

  unlinkPlex(): Observable<void> {
    return this.http.delete<void>('/api/account/plex/link');
  }
}
