import { HttpEvent, HttpHandlerFn, HttpRequest, HttpResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import { ApiError } from '@core/interceptors/error.interceptor';
import { AuthService, TokenResponse } from './auth.service';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  interface SetupOptions {
    accessToken?: string | null;
    expired?: boolean;
    refreshResult?: TokenResponse | null;
    hasRefreshToken?: boolean;
    authenticated?: boolean;
    responses?: Observable<HttpEvent<unknown>>[];
  }

  function apiError(statusCode: number): ApiError {
    const error = new ApiError(`failed with ${statusCode}`);
    error.statusCode = statusCode;
    return error;
  }

  function tokens(accessToken: string): TokenResponse {
    return { accessToken, refreshToken: 'refresh', expiresIn: 900 };
  }

  function setup(options: SetupOptions = {}) {
    const calls = { getAccessToken: 0, refreshToken: 0, logout: 0, expiryBuffers: [] as (number | undefined)[] };

    const auth = {
      getAccessToken: (): string | null => {
        calls.getAccessToken++;
        return options.accessToken ?? null;
      },
      isTokenExpired: (bufferSeconds?: number): boolean => {
        calls.expiryBuffers.push(bufferSeconds);
        return options.expired ?? false;
      },
      refreshToken: (): Observable<TokenResponse | null> => {
        calls.refreshToken++;
        return of(options.refreshResult ?? null);
      },
      hasRefreshToken: (): boolean => options.hasRefreshToken ?? false,
      isAuthenticated: (): boolean => options.authenticated ?? false,
      logout: (): void => {
        calls.logout++;
      },
    };

    TestBed.configureTestingModule({
      providers: [{ provide: AuthService, useValue: auth }],
    });

    const seen: HttpRequest<unknown>[] = [];
    const next: HttpHandlerFn = (req): Observable<HttpEvent<unknown>> => {
      seen.push(req);
      return options.responses?.[seen.length - 1] ?? of(new HttpResponse<unknown>());
    };

    const run = (url = '/api/queue') => {
      const req = new HttpRequest('GET', url);
      const events: HttpEvent<unknown>[] = [];
      let error: unknown = null;

      TestBed.runInInjectionContext(() => authInterceptor(req, next)).subscribe({
        next: (event) => events.push(event),
        error: (err: unknown) => {
          error = err;
        },
      });

      return { req, events, error: error as unknown };
    };

    return { calls, seen, run };
  }

  it('forwards auth endpoint requests untouched without consulting the token', () => {
    const { calls, seen, run } = setup({ accessToken: 'token' });

    const { req } = run('/api/auth/login');

    expect(seen[0]).toBe(req);
    expect(seen[0].headers.has('Authorization')).toBe(false);
    expect(calls.getAccessToken).toBe(0);
  });

  it('still recognises an auth endpoint behind a deployment base path', () => {
    const { calls, seen, run } = setup({ accessToken: 'token' });

    run('/cleanuparr/api/auth/login');

    expect(seen[0].headers.has('Authorization')).toBe(false);
    expect(calls.getAccessToken).toBe(0);
  });

  it('still recognises an auth endpoint given as an absolute url', () => {
    const { calls, seen, run } = setup({ accessToken: 'token' });

    run('http://localhost:5000/api/auth/login');

    expect(seen[0].headers.has('Authorization')).toBe(false);
    expect(calls.getAccessToken).toBe(0);
  });

  it('does not treat an auth path inside a query string as an auth endpoint', () => {
    const { seen, run } = setup({ accessToken: 'token' });

    run('/api/logs?filter=/api/auth/login');

    expect(seen[0].headers.get('Authorization')).toBe('Bearer token');
  });

  it('attaches a bearer token to an authenticated request', () => {
    const { seen, run } = setup({ accessToken: 'token' });

    run();

    expect(seen[0].headers.get('Authorization')).toBe('Bearer token');
  });

  it('forwards the request unchanged when no token is stored', () => {
    const { seen, calls, run } = setup({ accessToken: null });

    run();

    expect(seen[0].headers.has('Authorization')).toBe(false);
    expect(calls.refreshToken).toBe(0);
  });

  it('refreshes before sending when the token is about to expire', () => {
    const { calls, seen, run } = setup({
      accessToken: 'stale',
      expired: true,
      refreshResult: tokens('fresh'),
    });

    run();

    expect(calls.refreshToken).toBe(1);
    expect(calls.expiryBuffers).toEqual([30]);
    expect(seen).toHaveLength(1);
    expect(seen[0].headers.get('Authorization')).toBe('Bearer fresh');
  });

  it('logs out and surfaces a 401 when the pre-flight refresh yields nothing and no refresh token remains', () => {
    const { calls, seen, run } = setup({
      accessToken: 'stale',
      expired: true,
      refreshResult: null,
      hasRefreshToken: false,
    });

    const { error } = run();

    expect(calls.logout).toBe(1);
    expect(seen).toHaveLength(0);
    expect((error as ApiError).statusCode).toBe(401);
  });

  it('keeps the session when the pre-flight refresh yields nothing but a refresh token remains', () => {
    const { calls, run } = setup({
      accessToken: 'stale',
      expired: true,
      refreshResult: null,
      hasRefreshToken: true,
    });

    const { error } = run();

    expect(calls.logout).toBe(0);
    expect((error as ApiError).statusCode).toBe(401);
  });

  it('refreshes once and retries a 401 without refreshing again when the retry also fails', () => {
    const { calls, seen, run } = setup({
      accessToken: 'token',
      refreshResult: tokens('fresh'),
      responses: [throwError(() => apiError(401)), throwError(() => apiError(401))],
    });

    const { error } = run();

    expect(calls.refreshToken).toBe(1);
    expect(seen).toHaveLength(2);
    expect(seen[1].headers.get('Authorization')).toBe('Bearer fresh');
    expect((error as ApiError).statusCode).toBe(401);
  });

  it('rethrows a non-401 error without refreshing', () => {
    const { calls, seen, run } = setup({
      accessToken: 'token',
      responses: [throwError(() => apiError(500))],
    });

    const { error } = run();

    expect(calls.refreshToken).toBe(0);
    expect(seen).toHaveLength(1);
    expect((error as ApiError).statusCode).toBe(500);
  });
});
