import { HttpInterceptorFn, HttpErrorResponse, HttpContextToken, HttpContext } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

const IS_RETRY = new HttpContextToken<boolean>(() => false);

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
      // Only attempt refresh on 401, if we had a token, and this isn't already a retry
      if (error.status === 401 && token && !req.context.get(IS_RETRY)) {
        return auth.refreshToken().pipe(
          switchMap((result) => {
            if (result) {
              // Retry the request with the new token
              const retryReq = req.clone({
                setHeaders: { Authorization: `Bearer ${result.accessToken}` },
                context: new HttpContext().set(IS_RETRY, true),
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
