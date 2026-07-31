import { HttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { ApiError } from '@core/interceptors/error.interceptor';
import { AuthService, AuthStatus, TokenResponse } from './auth.service';

const NOW_MS = Date.UTC(2026, 6, 31, 12, 0, 0);
const NOW_SEC = Math.floor(NOW_MS / 1000);

describe('AuthService', () => {
  interface SetupOptions {
    status?: Observable<AuthStatus>;
    responses?: Record<string, Observable<unknown>>;
  }

  let active: AuthService | null = null;

  function jwt(secondsFromNow: number): string {
    return `header.${btoa(JSON.stringify({ exp: NOW_SEC + secondsFromNow }))}.signature`;
  }

  function base64UrlJwt(secondsFromNow: number): string {
    const encoded = btoa(JSON.stringify({ exp: NOW_SEC + secondsFromNow, pad: '??' }));
    return `header.${encoded.replace(/\+/g, '-').replace(/\//g, '_')}.signature`;
  }

  function tokens(accessToken: string, refreshToken = 'refresh-token'): TokenResponse {
    return { accessToken, refreshToken, expiresIn: 900 };
  }

  function apiError(statusCode: number): ApiError {
    const error = new ApiError(`failed with ${statusCode}`);
    error.statusCode = statusCode;
    return error;
  }

  function status(overrides: Partial<AuthStatus> = {}): AuthStatus {
    return { setupCompleted: true, plexLinked: false, ...overrides };
  }

  function setup(options: SetupOptions = {}) {
    const gets: string[] = [];
    const posts: { url: string; body: unknown }[] = [];
    const navigations: unknown[][] = [];

    const http = {
      get: (url: string): Observable<unknown> => {
        gets.push(url);
        return options.status ?? of(status());
      },
      post: (url: string, body: unknown): Observable<unknown> => {
        posts.push({ url, body });
        return options.responses?.[url] ?? of({});
      },
    };

    const router = {
      navigate: (commands: unknown[]): Promise<boolean> => {
        navigations.push(commands);
        return Promise.resolve(true);
      },
    };

    TestBed.configureTestingModule({
      providers: [
        { provide: HttpClient, useValue: http },
        { provide: Router, useValue: router },
      ],
    });

    const service = TestBed.inject(AuthService);
    active = service;

    const postsTo = (url: string) => posts.filter((post) => post.url === url);

    return { service, gets, posts, postsTo, navigations };
  }

  beforeEach(() => {
    localStorage.clear();
    vi.useFakeTimers();
    vi.setSystemTime(NOW_MS);
  });

  afterEach(() => {
    if (active) {
      active.logout();
      active = null;
    }
    vi.useRealTimers();
    localStorage.clear();
  });

  describe('isTokenExpired', () => {
    it('treats a missing token as expired', () => {
      const { service } = setup();

      expect(service.isTokenExpired()).toBe(true);
    });

    it('reports a token living well beyond the buffer as valid', () => {
      localStorage.setItem('access_token', jwt(600));
      const { service } = setup();

      expect(service.isTokenExpired(60)).toBe(false);
    });

    it('reports a token expiring inside the buffer window as expired', () => {
      localStorage.setItem('access_token', jwt(45));
      const { service } = setup();

      expect(service.isTokenExpired(60)).toBe(true);
      expect(service.isTokenExpired(30)).toBe(false);
    });

    it('defaults to a thirty second buffer', () => {
      localStorage.setItem('access_token', jwt(29));
      const { service } = setup();

      expect(service.isTokenExpired()).toBe(true);
    });

    it('treats the exact buffer boundary as expired', () => {
      localStorage.setItem('access_token', jwt(30));
      const { service } = setup();

      expect(service.isTokenExpired(30)).toBe(true);
    });

    it('decodes a base64url payload containing substituted characters', () => {
      const token = base64UrlJwt(600);
      expect(token).toContain('_');

      localStorage.setItem('access_token', token);
      const { service } = setup();

      expect(service.isTokenExpired(60)).toBe(false);
    });

    it('treats a token without a payload segment as expired', () => {
      localStorage.setItem('access_token', 'garbage');
      const { service } = setup();

      expect(service.isTokenExpired()).toBe(true);
    });

    it('treats an undecodable payload as expired', () => {
      localStorage.setItem('access_token', 'header.!!!not-base64!!!.signature');
      const { service } = setup();

      expect(service.isTokenExpired()).toBe(true);
    });

    it('treats a payload that is not json as expired', () => {
      localStorage.setItem('access_token', `header.${btoa('plain text')}.signature`);
      const { service } = setup();

      expect(service.isTokenExpired()).toBe(true);
    });

    it('treats a non numeric exp claim as expired', () => {
      localStorage.setItem('access_token', `header.${btoa(JSON.stringify({ exp: 'soon' }))}.sig`);
      const { service } = setup();

      expect(service.isTokenExpired()).toBe(true);
    });
  });

  describe('token storage', () => {
    it('reads back the stored access token', () => {
      localStorage.setItem('access_token', 'stored');
      const { service } = setup();

      expect(service.getAccessToken()).toBe('stored');
    });

    it('reports no access token when none is stored', () => {
      const { service } = setup();

      expect(service.getAccessToken()).toBeNull();
    });

    it('reports whether a refresh token is stored', () => {
      const { service } = setup();

      expect(service.hasRefreshToken()).toBe(false);

      localStorage.setItem('refresh_token', 'refresh');

      expect(service.hasRefreshToken()).toBe(true);
    });
  });

  describe('login', () => {
    it('stores tokens and authenticates when two factor is not required', () => {
      const { service } = setup({
        responses: {
          '/api/auth/login': of({ requiresTwoFactor: false, tokens: tokens(jwt(600), 'r1') }),
        },
      });

      service.login('user', 'pass').subscribe();

      expect(localStorage.getItem('access_token')).toBe(jwt(600));
      expect(localStorage.getItem('refresh_token')).toBe('r1');
      expect(service.isAuthenticated()).toBe(true);
    });

    it('withholds authentication while two factor is pending', () => {
      const { service } = setup({
        responses: {
          '/api/auth/login': of({ requiresTwoFactor: true, loginToken: 'login-token' }),
        },
      });

      service.login('user', 'pass').subscribe();

      expect(localStorage.getItem('access_token')).toBeNull();
      expect(service.isAuthenticated()).toBe(false);
    });

    it('authenticates once the two factor code is verified', () => {
      const { service, posts } = setup({
        responses: { '/api/auth/login/2fa': of(tokens(jwt(600), 'r2')) },
      });

      service.verify2fa('login-token', '123456').subscribe();

      expect(posts[0].body).toEqual({
        loginToken: 'login-token',
        code: '123456',
        isRecoveryCode: false,
      });
      expect(service.isAuthenticated()).toBe(true);
      expect(localStorage.getItem('refresh_token')).toBe('r2');
    });

    it('forwards the recovery code flag', () => {
      const { service, posts } = setup({
        responses: { '/api/auth/login/2fa': of(tokens(jwt(600))) },
      });

      service.verify2fa('login-token', 'recovery', true).subscribe();

      expect(posts[0].body).toEqual({
        loginToken: 'login-token',
        code: 'recovery',
        isRecoveryCode: true,
      });
    });

    it('authenticates when the plex pin verification completes with tokens', () => {
      const { service } = setup({
        responses: {
          '/api/auth/login/plex/verify': of({ completed: true, tokens: tokens(jwt(600)) }),
        },
      });

      service.verifyPlexPin(42).subscribe();

      expect(service.isAuthenticated()).toBe(true);
    });

    it('stays unauthenticated while the plex pin is still pending', () => {
      const { service } = setup({
        responses: { '/api/auth/login/plex/verify': of({ completed: false }) },
      });

      service.verifyPlexPin(42).subscribe();

      expect(service.isAuthenticated()).toBe(false);
      expect(localStorage.getItem('access_token')).toBeNull();
    });

    it('authenticates when the oidc code is exchanged', () => {
      const { service } = setup({
        responses: { '/api/auth/oidc/exchange': of(tokens(jwt(600), 'r3')) },
      });

      service.exchangeOidcCode('code').subscribe();

      expect(service.isAuthenticated()).toBe(true);
      expect(localStorage.getItem('refresh_token')).toBe('r3');
    });
  });

  describe('setup flow', () => {
    it('marks setup complete only once the server confirms', () => {
      const { service, posts } = setup({
        responses: { '/api/auth/setup/complete': of({ message: 'ok' }) },
      });

      expect(service.isSetupComplete()).toBe(false);

      service.completeSetup().subscribe();

      expect(posts[0].url).toBe('/api/auth/setup/complete');
      expect(service.isSetupComplete()).toBe(true);
    });

    it('posts the credentials when creating the account', () => {
      const { service, posts } = setup({
        responses: { '/api/auth/setup/account': of({ userId: 'u1' }) },
      });

      service.createAccount('user', 'pass').subscribe();

      expect(posts[0]).toEqual({
        url: '/api/auth/setup/account',
        body: { username: 'user', password: 'pass' },
      });
    });

    it('does not authenticate from the two factor setup endpoints', () => {
      const { service, posts } = setup({
        responses: {
          '/api/auth/setup/2fa/generate': of({ secret: 's', qrCodeUri: 'q', recoveryCodes: [] }),
          '/api/auth/setup/2fa/verify': of({ message: 'ok' }),
        },
      });

      service.generateTotpSetup().subscribe();
      service.verifyTotpSetup('123456').subscribe();

      expect(posts.map((post) => post.url)).toEqual([
        '/api/auth/setup/2fa/generate',
        '/api/auth/setup/2fa/verify',
      ]);
      expect(service.isAuthenticated()).toBe(false);
    });
  });

  describe('checkStatus', () => {
    it('publishes every flag from the response', () => {
      const { service } = setup({
        status: of(
          status({
            setupCompleted: true,
            plexLinked: true,
            oidcEnabled: true,
            oidcProviderName: 'Authelia',
            oidcExclusiveMode: true,
          }),
        ),
      });

      service.checkStatus().subscribe();

      expect(service.isSetupComplete()).toBe(true);
      expect(service.plexLinked()).toBe(true);
      expect(service.oidcEnabled()).toBe(true);
      expect(service.oidcProviderName()).toBe('Authelia');
      expect(service.oidcExclusiveMode()).toBe(true);
      expect(service.isLoading()).toBe(false);
    });

    it('defaults the optional oidc flags when the server omits them', () => {
      const { service } = setup({ status: of(status()) });

      service.checkStatus().subscribe();

      expect(service.oidcEnabled()).toBe(false);
      expect(service.oidcProviderName()).toBe('');
      expect(service.oidcExclusiveMode()).toBe(false);
    });

    it('authenticates without any token when the trusted network bypass is active', () => {
      const { service, postsTo } = setup({
        status: of(status({ authBypassActive: true })),
      });

      service.checkStatus().subscribe();

      expect(service.isAuthenticated()).toBe(true);
      expect(service.isLoading()).toBe(false);
      expect(postsTo('/api/auth/refresh')).toHaveLength(0);
    });

    it('ignores the bypass while setup is incomplete', () => {
      const { service } = setup({
        status: of(status({ setupCompleted: false, authBypassActive: true })),
      });

      service.checkStatus().subscribe();

      expect(service.isAuthenticated()).toBe(false);
    });

    it('authenticates from a still valid stored token', () => {
      localStorage.setItem('access_token', jwt(600));
      const { service, postsTo } = setup({ status: of(status()) });

      service.checkStatus().subscribe();

      expect(service.isAuthenticated()).toBe(true);
      expect(service.isLoading()).toBe(false);
      expect(postsTo('/api/auth/refresh')).toHaveLength(0);
    });

    it('stays unauthenticated when setup is incomplete even with a stored token', () => {
      localStorage.setItem('access_token', jwt(600));
      const { service } = setup({ status: of(status({ setupCompleted: false })) });

      service.checkStatus().subscribe();

      expect(service.isAuthenticated()).toBe(false);
      expect(service.isLoading()).toBe(false);
    });

    it('stays unauthenticated when nothing is stored', () => {
      const { service } = setup({ status: of(status()) });

      service.checkStatus().subscribe();

      expect(service.isAuthenticated()).toBe(false);
      expect(service.isLoading()).toBe(false);
    });

    it('refreshes a nearly expired stored token and authenticates on success', () => {
      localStorage.setItem('access_token', jwt(30));
      localStorage.setItem('refresh_token', 'stored-refresh');
      const { service, postsTo, navigations } = setup({
        status: of(status()),
        responses: { '/api/auth/refresh': of(tokens(jwt(900), 'rotated')) },
      });

      service.checkStatus().subscribe();

      expect(postsTo('/api/auth/refresh')).toHaveLength(1);
      expect(service.isAuthenticated()).toBe(true);
      expect(service.isLoading()).toBe(false);
      expect(localStorage.getItem('refresh_token')).toBe('rotated');
      expect(navigations).toHaveLength(0);
    });

    it('redirects to login when the stored token is expired and cannot be refreshed', () => {
      localStorage.setItem('access_token', jwt(30));
      const { service, navigations } = setup({ status: of(status()) });

      service.checkStatus().subscribe();

      expect(service.isAuthenticated()).toBe(false);
      expect(service.isLoading()).toBe(false);
      expect(navigations).toEqual([['/auth/login']]);
    });

    it('flags a connection error and emits an empty status when the request fails', () => {
      const { service } = setup({ status: throwError(() => apiError(503)) });

      const received: AuthStatus[] = [];
      service.checkStatus().subscribe((value) => received.push(value));

      expect(service.connectionError()).toBe(true);
      expect(service.isLoading()).toBe(false);
      expect(received).toEqual([{ setupCompleted: false, plexLinked: false }]);
    });

    it('clears a previous connection error on a successful retry', () => {
      let response: Observable<AuthStatus> = throwError(() => apiError(503));
      const http = {
        get: (): Observable<unknown> => response,
        post: (): Observable<unknown> => of({}),
      };
      TestBed.configureTestingModule({
        providers: [
          { provide: HttpClient, useValue: http },
          { provide: Router, useValue: { navigate: () => Promise.resolve(true) } },
        ],
      });
      const service = TestBed.inject(AuthService);
      active = service;

      service.checkStatus().subscribe();
      expect(service.connectionError()).toBe(true);

      response = of(status());
      service.retryConnection().subscribe();

      expect(service.connectionError()).toBe(false);
      expect(service.isLoading()).toBe(false);
    });

    it('requests the status endpoint', () => {
      const { service, gets } = setup();

      service.checkStatus().subscribe();

      expect(gets).toEqual(['/api/auth/status']);
    });
  });

  describe('refreshToken', () => {
    it('resolves to null without calling the server when no refresh token is stored', () => {
      const { service, posts } = setup();

      let result: TokenResponse | null | undefined;
      service.refreshToken().subscribe((value) => {
        result = value;
      });

      expect(result).toBeNull();
      expect(posts).toHaveLength(0);
    });

    it('stores the rotated tokens and authenticates', () => {
      localStorage.setItem('refresh_token', 'old-refresh');
      const { service, posts } = setup({
        responses: { '/api/auth/refresh': of(tokens(jwt(900), 'new-refresh')) },
      });

      service.refreshToken().subscribe();

      expect(posts[0]).toEqual({
        url: '/api/auth/refresh',
        body: { refreshToken: 'old-refresh' },
      });
      expect(localStorage.getItem('access_token')).toBe(jwt(900));
      expect(localStorage.getItem('refresh_token')).toBe('new-refresh');
      expect(service.isAuthenticated()).toBe(true);
    });

    it('shares a single in-flight request between concurrent callers', () => {
      localStorage.setItem('refresh_token', 'old-refresh');
      const { service, postsTo } = setup({
        responses: { '/api/auth/refresh': of(tokens(jwt(900))) },
      });

      const first = service.refreshToken();
      const second = service.refreshToken();

      expect(second).toBe(first);

      const results: (TokenResponse | null)[] = [];
      first.subscribe((value) => results.push(value));
      second.subscribe((value) => results.push(value));

      expect(postsTo('/api/auth/refresh')).toHaveLength(1);
      expect(results).toHaveLength(2);
      expect(results[0]).toBe(results[1]);
    });

    it('starts a new request once the previous refresh has settled', () => {
      localStorage.setItem('refresh_token', 'old-refresh');
      const { service, postsTo } = setup({
        responses: { '/api/auth/refresh': of(tokens(jwt(900))) },
      });

      service.refreshToken().subscribe();
      service.refreshToken().subscribe();

      expect(postsTo('/api/auth/refresh')).toHaveLength(2);
    });

    it('clears the session when the server definitively rejects the refresh token', () => {
      localStorage.setItem('access_token', jwt(600));
      localStorage.setItem('refresh_token', 'revoked');
      const { service } = setup({
        responses: { '/api/auth/refresh': throwError(() => apiError(401)) },
      });

      let result: TokenResponse | null | undefined;
      service.refreshToken().subscribe((value) => {
        result = value;
      });

      expect(result).toBeNull();
      expect(localStorage.getItem('access_token')).toBeNull();
      expect(localStorage.getItem('refresh_token')).toBeNull();
      expect(service.isAuthenticated()).toBe(false);
    });

    it('keeps the stored tokens when the refresh fails for a transient reason', () => {
      localStorage.setItem('access_token', jwt(600));
      localStorage.setItem('refresh_token', 'still-good');
      const { service } = setup({
        responses: { '/api/auth/refresh': throwError(() => apiError(503)) },
      });

      let result: TokenResponse | null | undefined;
      service.refreshToken().subscribe((value) => {
        result = value;
      });

      expect(result).toBeNull();
      expect(localStorage.getItem('refresh_token')).toBe('still-good');
    });
  });

  describe('refresh scheduling', () => {
    it('schedules a refresh at eighty percent of the remaining lifetime', () => {
      const { service, postsTo } = setup({
        responses: {
          '/api/auth/login': of({ requiresTwoFactor: false, tokens: tokens(jwt(1000)) }),
          '/api/auth/refresh': of(tokens(jwt(2000))),
        },
      });

      service.login('user', 'pass').subscribe();

      vi.advanceTimersByTime(799_999);
      expect(postsTo('/api/auth/refresh')).toHaveLength(0);

      vi.advanceTimersByTime(1);
      expect(postsTo('/api/auth/refresh')).toHaveLength(1);
    });

    it('refreshes immediately when the fresh token already sits inside the guard window', () => {
      const { service, postsTo } = setup({
        responses: {
          '/api/auth/login': of({ requiresTwoFactor: false, tokens: tokens(jwt(10)) }),
          '/api/auth/refresh': of(tokens(jwt(900))),
        },
      });

      service.login('user', 'pass').subscribe();

      expect(postsTo('/api/auth/refresh')).toHaveLength(1);
    });

    it('replaces the pending timer when a newer token arrives', () => {
      const calls: string[] = [];
      let loginTokens = tokens(jwt(1000), 'r1');
      const http = {
        get: (): Observable<unknown> => of(status()),
        post: (url: string): Observable<unknown> => {
          calls.push(url);
          if (url === '/api/auth/login') {
            return of({ requiresTwoFactor: false, tokens: loginTokens });
          }
          return of(tokens(jwt(5000)));
        },
      };
      TestBed.configureTestingModule({
        providers: [
          { provide: HttpClient, useValue: http },
          { provide: Router, useValue: { navigate: () => Promise.resolve(true) } },
        ],
      });
      const service = TestBed.inject(AuthService);
      active = service;

      service.login('user', 'pass').subscribe();
      loginTokens = tokens(jwt(2000), 'r2');
      service.login('user', 'pass').subscribe();

      vi.advanceTimersByTime(800_000);
      expect(calls.filter((url) => url === '/api/auth/refresh')).toHaveLength(0);

      vi.advanceTimersByTime(800_000);
      expect(calls.filter((url) => url === '/api/auth/refresh')).toHaveLength(1);
    });

    it('does not schedule anything from an undecodable access token', () => {
      const { service, postsTo } = setup({
        responses: {
          '/api/auth/login': of({
            requiresTwoFactor: false,
            tokens: tokens('not-a-jwt'),
          }),
        },
      });

      service.login('user', 'pass').subscribe();

      vi.advanceTimersByTime(10_000_000);
      expect(postsTo('/api/auth/refresh')).toHaveLength(0);
    });

    it('cancels the pending refresh on logout', () => {
      const { service, postsTo } = setup({
        responses: {
          '/api/auth/login': of({ requiresTwoFactor: false, tokens: tokens(jwt(1000)) }),
        },
      });

      service.login('user', 'pass').subscribe();
      service.logout();

      vi.advanceTimersByTime(10_000_000);
      expect(postsTo('/api/auth/refresh')).toHaveLength(0);
    });
  });

  describe('logout', () => {
    it('revokes the refresh token server side and clears the local session', () => {
      localStorage.setItem('access_token', jwt(600));
      localStorage.setItem('refresh_token', 'to-revoke');
      const { service, posts, navigations } = setup();
      service.checkStatus().subscribe();

      service.logout();

      expect(posts).toEqual([{ url: '/api/auth/logout', body: { refreshToken: 'to-revoke' } }]);
      expect(localStorage.getItem('access_token')).toBeNull();
      expect(localStorage.getItem('refresh_token')).toBeNull();
      expect(service.isAuthenticated()).toBe(false);
      expect(navigations).toEqual([['/auth/login']]);
    });

    it('skips the revocation call when no refresh token is stored', () => {
      const { service, posts, navigations } = setup();

      service.logout();

      expect(posts).toHaveLength(0);
      expect(navigations).toEqual([['/auth/login']]);
    });

    it('clears the session even when the revocation call fails', () => {
      localStorage.setItem('refresh_token', 'to-revoke');
      const { service, navigations } = setup({
        responses: { '/api/auth/logout': throwError(() => apiError(500)) },
      });

      service.logout();

      expect(localStorage.getItem('refresh_token')).toBeNull();
      expect(navigations).toEqual([['/auth/login']]);
    });
  });

  describe('visibility recovery', () => {
    function becomeVisible(): void {
      document.dispatchEvent(new Event('visibilitychange'));
    }

    it('refreshes a token that expired while the tab was hidden', () => {
      const { service, postsTo } = setup({
        responses: {
          '/api/auth/login': of({ requiresTwoFactor: false, tokens: tokens(jwt(1000)) }),
          '/api/auth/refresh': of(tokens(jwt(2000))),
        },
      });
      service.login('user', 'pass').subscribe();

      vi.setSystemTime(NOW_MS + 990_000);
      becomeVisible();

      expect(postsTo('/api/auth/refresh')).toHaveLength(1);
      expect(service.isAuthenticated()).toBe(true);
    });

    it('redirects to login when the post sleep refresh is rejected', () => {
      const { service, navigations } = setup({
        responses: {
          '/api/auth/login': of({ requiresTwoFactor: false, tokens: tokens(jwt(1000)) }),
          '/api/auth/refresh': throwError(() => apiError(401)),
        },
      });
      service.login('user', 'pass').subscribe();

      vi.setSystemTime(NOW_MS + 990_000);
      becomeVisible();

      expect(navigations).toEqual([['/auth/login']]);
      expect(service.isAuthenticated()).toBe(false);
    });

    it('reschedules the frozen timer when the token is still valid', () => {
      const { service, postsTo } = setup({
        responses: {
          '/api/auth/login': of({ requiresTwoFactor: false, tokens: tokens(jwt(1000)) }),
          '/api/auth/refresh': of(tokens(jwt(5000))),
        },
      });
      service.login('user', 'pass').subscribe();

      vi.setSystemTime(NOW_MS + 500_000);
      becomeVisible();

      expect(postsTo('/api/auth/refresh')).toHaveLength(0);

      vi.advanceTimersByTime(399_999);
      expect(postsTo('/api/auth/refresh')).toHaveLength(0);

      vi.advanceTimersByTime(1);
      expect(postsTo('/api/auth/refresh')).toHaveLength(1);
    });

    it('ignores the event while the tab is hidden', () => {
      const { service, postsTo } = setup({
        responses: {
          '/api/auth/login': of({ requiresTwoFactor: false, tokens: tokens(jwt(1000)) }),
          '/api/auth/refresh': of(tokens(jwt(2000))),
        },
      });
      service.login('user', 'pass').subscribe();

      Object.defineProperty(document, 'visibilityState', {
        value: 'hidden',
        configurable: true,
      });

      vi.setSystemTime(NOW_MS + 990_000);
      becomeVisible();

      expect(postsTo('/api/auth/refresh')).toHaveLength(0);

      delete (document as unknown as Record<string, unknown>)['visibilityState'];
      expect(document.visibilityState).toBe('visible');
    });

    it('stops listening once the session is cleared', () => {
      const { service, postsTo } = setup({
        responses: {
          '/api/auth/login': of({ requiresTwoFactor: false, tokens: tokens(jwt(1000)) }),
          '/api/auth/refresh': of(tokens(jwt(2000))),
        },
      });
      service.login('user', 'pass').subscribe();
      service.logout();

      vi.setSystemTime(NOW_MS + 990_000);
      becomeVisible();

      expect(postsTo('/api/auth/refresh')).toHaveLength(0);
    });
  });
});
