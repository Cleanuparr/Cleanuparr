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

  // Pre-flight: if token is expired, refresh before sending the request
  if (auth.getAccessToken() && auth.isTokenExpired(30)) {
    return auth.refreshToken().pipe(
      switchMap((result) => {
        if (result) {
          const freshReq = req.clone({
            setHeaders: { Authorization: `Bearer ${result.accessToken}` },
            context: new HttpContext().set(IS_RETRY, true),
          });
          return next(freshReq);
        }
        auth.logout();
        return throwError(() => new HttpErrorResponse({ status: 401 }));
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
    catchError((error: HttpErrorResponse) => {
      // Fallback: 401 catch for edge cases (e.g., token expired between check and send)
      if (error.status === 401 && token && !req.context.get(IS_RETRY)) {
        return auth.refreshToken().pipe(
          switchMap((result) => {
            if (result) {
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
