import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap, of, catchError, finalize, shareReplay } from 'rxjs';
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
  private refreshInFlight$: Observable<TokenResponse | null> | null = null;
  private visibilityHandler: (() => void) | null = null;

  checkStatus(): Observable<AuthStatus> {
    return this.http.get<AuthStatus>('/api/auth/status').pipe(
      tap((status) => {
        this._isSetupComplete.set(status.setupCompleted);
        this._plexLinked.set(status.plexLinked);

        const token = localStorage.getItem('access_token');
        if (token && status.setupCompleted) {
          if (this.isTokenExpired(60)) {
            // Access token expired — try to refresh before marking as authenticated
            this.refreshToken().subscribe((result) => {
              if (result) {
                this._isAuthenticated.set(true);
                this.setupVisibilityListener();
              } else {
                this._isAuthenticated.set(false);
                this.router.navigate(['/auth/login']);
              }
              this._isLoading.set(false);
            });
            return;
          }

          this._isAuthenticated.set(true);
          this.scheduleRefresh();
          this.setupVisibilityListener();
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
    // Deduplicate: if a refresh is already in-flight, share the same observable
    if (this.refreshInFlight$) {
      return this.refreshInFlight$;
    }

    const storedRefreshToken = localStorage.getItem('refresh_token');
    if (!storedRefreshToken) {
      this.clearAuth();
      return of(null);
    }

    this.refreshInFlight$ = this.http
      .post<TokenResponse>('/api/auth/refresh', { refreshToken: storedRefreshToken })
      .pipe(
        tap((tokens) => this.handleTokens(tokens)),
        catchError(() => {
          this.clearAuth();
          return of(null);
        }),
        finalize(() => {
          this.refreshInFlight$ = null;
        }),
        shareReplay(1),
      );

    return this.refreshInFlight$;
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

  /** Returns true if the access token is expired or will expire within the buffer period. */
  isTokenExpired(bufferSeconds = 30): boolean {
    const token = localStorage.getItem('access_token');
    if (!token) return true;

    const exp = this.getTokenExpiry(token);
    if (exp === null) return true;

    return Date.now() / 1000 >= exp - bufferSeconds;
  }

  private handleTokens(tokens: TokenResponse): void {
    localStorage.setItem('access_token', tokens.accessToken);
    localStorage.setItem('refresh_token', tokens.refreshToken);
    this._isAuthenticated.set(true);
    this.scheduleRefresh();
    this.setupVisibilityListener();
  }

  private scheduleRefresh(): void {
    if (this.refreshTimer) {
      clearTimeout(this.refreshTimer);
    }

    // Always derive from the JWT's actual exp claim — never trust ExpiresIn from response
    const token = localStorage.getItem('access_token');
    if (!token) return;

    const exp = this.getTokenExpiry(token);
    if (exp === null) return;

    const remainingSec = exp - Date.now() / 1000;
    if (remainingSec <= 30) {
      this.refreshToken().subscribe();
      return;
    }

    // Refresh at 80% of remaining lifetime
    const refreshMs = remainingSec * 800;
    this.refreshTimer = setTimeout(() => {
      this.refreshToken().subscribe();
    }, refreshMs);
  }

  private clearAuth(): void {
    localStorage.removeItem('access_token');
    localStorage.removeItem('refresh_token');
    this._isAuthenticated.set(false);
    this.refreshInFlight$ = null;

    if (this.refreshTimer) {
      clearTimeout(this.refreshTimer);
      this.refreshTimer = null;
    }

    this.teardownVisibilityListener();
  }

  /** Extracts the exp claim (seconds since epoch) from a JWT. */
  private getTokenExpiry(token: string): number | null {
    try {
      const payload = token.split('.')[1];
      if (!payload) return null;
      const decoded = atob(payload.replace(/-/g, '+').replace(/_/g, '/'));
      const parsed = JSON.parse(decoded);
      return typeof parsed.exp === 'number' ? parsed.exp : null;
    } catch {
      return null;
    }
  }

  private setupVisibilityListener(): void {
    if (this.visibilityHandler) return;

    this.visibilityHandler = () => {
      if (document.visibilityState !== 'visible' || !this._isAuthenticated()) {
        return;
      }

      if (this.isTokenExpired(60)) {
        // Token expired during sleep — refresh immediately
        this.refreshToken().subscribe((result) => {
          if (!result) {
            this.router.navigate(['/auth/login']);
          }
        });
      } else {
        // Token still valid — reschedule timer (old setTimeout was frozen during sleep)
        this.scheduleRefresh();
      }
    };

    document.addEventListener('visibilitychange', this.visibilityHandler);
  }

  private teardownVisibilityListener(): void {
    if (this.visibilityHandler) {
      document.removeEventListener('visibilitychange', this.visibilityHandler);
      this.visibilityHandler = null;
    }
  }
}
