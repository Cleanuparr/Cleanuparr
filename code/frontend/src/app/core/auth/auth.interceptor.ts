import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';
import { ApiError } from '@core/interceptors/error.interceptor';

// The baseUrl interceptor runs first. It can add a base path prefix to the URL.
// The auth segment can be at any position in the path.
function isAuthEndpoint(url: string): boolean {
  const path = url.startsWith('http') ? new URL(url).pathname : url.split('?')[0];
  return path.includes('/api/auth/');
}

function unauthorized(): ApiError {
  const error = new ApiError('Unauthorized');
  error.statusCode = 401;
  return error;
}

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);

  // Skip auth header for auth endpoints
  if (isAuthEndpoint(req.url)) {
    return next(req);
  }

  // Pre-flight: if token is expired, refresh before sending the request
  if (auth.getAccessToken() && auth.isTokenExpired(30)) {
    return auth.refreshToken().pipe(
      switchMap((result) => {
        if (result) {
          const freshReq = req.clone({
            setHeaders: { Authorization: `Bearer ${result.accessToken}` },
          });
          return next(freshReq);
        }
        if (!auth.hasRefreshToken()) {
          auth.logout();
        }
        return throwError(() => unauthorized());
      }),
    );
  }

  // Normal path: token is valid
  const token = auth.getAccessToken();
  if (token) {
    req = req.clone({
      setHeaders: { Authorization: `Bearer ${token}` },
    });
  }

  return next(req).pipe(
    catchError((error) => {
      // Fallback: 401 catch for edge cases (e.g., token expired between check and send)
      if ((error as ApiError).statusCode === 401 && token) {
        return auth.refreshToken().pipe(
          switchMap((result) => {
            if (result) {
              const retryReq = req.clone({
                setHeaders: { Authorization: `Bearer ${result.accessToken}` },
              });
              return next(retryReq);
            }
            if (!auth.isAuthenticated()) {
              auth.logout();
            }
            return throwError(() => error);
          }),
        );
      }
      return throwError(() => error);
    }),
  );
};
