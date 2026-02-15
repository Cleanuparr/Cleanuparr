import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);

  // Skip auth header for auth endpoints
  if (req.url.includes('/api/auth/')) {
    return next(req);
  }

  const token = auth.getAccessToken();
  if (token) {
    req = req.clone({
      setHeaders: { Authorization: `Bearer ${token}` },
    });
  }

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401 && token) {
        // Try to refresh the token
        return auth.refreshToken().pipe(
          switchMap((result) => {
            if (result) {
              // Retry the request with the new token
              const retryReq = req.clone({
                setHeaders: { Authorization: `Bearer ${result.accessToken}` },
              });
              return next(retryReq);
            }
            auth.logout();
            return throwError(() => error);
          }),
        );
      }
      return throwError(() => error);
    }),
  );
};
