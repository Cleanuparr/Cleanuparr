import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap, of, catchError } from 'rxjs';
import { Router } from '@angular/router';

export interface AuthStatus {
  setupCompleted: boolean;
  plexLinked: boolean;
}

export interface LoginResponse {
  requiresTwoFactor: boolean;
  loginToken?: string;
}

export interface TokenResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
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

export interface PlexVerifyResponse {
  completed: boolean;
  tokens?: TokenResponse;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly _isAuthenticated = signal(false);
  private readonly _isSetupComplete = signal(false);
  private readonly _plexLinked = signal(false);
  private readonly _isLoading = signal(true);

  readonly isAuthenticated = this._isAuthenticated.asReadonly();
  readonly isSetupComplete = this._isSetupComplete.asReadonly();
  readonly plexLinked = this._plexLinked.asReadonly();
  readonly isLoading = this._isLoading.asReadonly();

  private refreshTimer: ReturnType<typeof setTimeout> | null = null;

  checkStatus(): Observable<AuthStatus> {
    return this.http.get<AuthStatus>('/api/auth/status').pipe(
      tap((status) => {
        this._isSetupComplete.set(status.setupCompleted);
        this._plexLinked.set(status.plexLinked);

        // Check if we have a valid token
        const token = localStorage.getItem('access_token');
        if (token && status.setupCompleted) {
          this._isAuthenticated.set(true);
          this.scheduleRefresh();
        }

        this._isLoading.set(false);
      }),
      catchError(() => {
        this._isLoading.set(false);
        return of({ setupCompleted: false, plexLinked: false });
      }),
    );
  }

  // Setup flow
  createAccount(username: string, password: string): Observable<{ userId: string }> {
    return this.http.post<{ userId: string }>('/api/auth/setup/account', { username, password });
  }

  generateTotpSetup(): Observable<TotpSetupResponse> {
    return this.http.post<TotpSetupResponse>('/api/auth/setup/2fa/generate', {});
  }

  verifyTotpSetup(code: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>('/api/auth/setup/2fa/verify', { code });
  }

  completeSetup(): Observable<{ message: string }> {
    return this.http.post<{ message: string }>('/api/auth/setup/complete', {}).pipe(
      tap(() => this._isSetupComplete.set(true)),
    );
  }

  // Login flow
  login(username: string, password: string): Observable<LoginResponse> {
    return this.http.post<LoginResponse>('/api/auth/login', { username, password });
  }

  verify2fa(loginToken: string, code: string, isRecoveryCode = false): Observable<TokenResponse> {
    return this.http
      .post<TokenResponse>('/api/auth/login/2fa', { loginToken, code, isRecoveryCode })
      .pipe(tap((tokens) => this.handleTokens(tokens)));
  }

  // Setup Plex linking
  requestSetupPlexPin(): Observable<PlexPinResponse> {
    return this.http.post<PlexPinResponse>('/api/auth/setup/plex/pin', {});
  }

  verifySetupPlexPin(pinId: number): Observable<PlexVerifyResponse> {
    return this.http.post<PlexVerifyResponse>('/api/auth/setup/plex/verify', { pinId });
  }

  // Plex login
  requestPlexPin(): Observable<PlexPinResponse> {
    return this.http.post<PlexPinResponse>('/api/auth/login/plex/pin', {});
  }

  verifyPlexPin(pinId: number): Observable<PlexVerifyResponse> {
    return this.http.post<PlexVerifyResponse>('/api/auth/login/plex/verify', { pinId }).pipe(
      tap((result) => {
        if (result.completed && result.tokens) {
          this.handleTokens(result.tokens);
        }
      }),
    );
  }

  // Token management
  refreshToken(): Observable<TokenResponse | null> {
    const refreshToken = localStorage.getItem('refresh_token');
    if (!refreshToken) {
      this.clearAuth();
      return of(null);
    }

    return this.http.post<TokenResponse>('/api/auth/refresh', { refreshToken }).pipe(
      tap((tokens) => this.handleTokens(tokens)),
      catchError(() => {
        this.clearAuth();
        return of(null);
      }),
    );
  }

  logout(): void {
    const refreshToken = localStorage.getItem('refresh_token');
    if (refreshToken) {
      this.http.post('/api/auth/logout', { refreshToken }).subscribe();
    }
    this.clearAuth();
    this.router.navigate(['/auth/login']);
  }

  getAccessToken(): string | null {
    return localStorage.getItem('access_token');
  }

  private handleTokens(tokens: TokenResponse): void {
    localStorage.setItem('access_token', tokens.accessToken);
    localStorage.setItem('refresh_token', tokens.refreshToken);
    this._isAuthenticated.set(true);
    this.scheduleRefresh(tokens.expiresIn);
  }

  private scheduleRefresh(expiresInSeconds = 900): void {
    if (this.refreshTimer) {
      clearTimeout(this.refreshTimer);
    }

    // Refresh at 80% of lifetime
    const refreshMs = expiresInSeconds * 800;
    this.refreshTimer = setTimeout(() => {
      this.refreshToken().subscribe();
    }, refreshMs);
  }

  private clearAuth(): void {
    localStorage.removeItem('access_token');
    localStorage.removeItem('refresh_token');
    this._isAuthenticated.set(false);
    if (this.refreshTimer) {
      clearTimeout(this.refreshTimer);
      this.refreshTimer = null;
    }
  }
}
