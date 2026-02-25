import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';

export class ApiError extends Error {
  retryAfterSeconds?: number;
  statusCode?: number;
}

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      let message = 'An unexpected error occurred';

      if (error.error instanceof ErrorEvent) {
        // Client-side error
        message = error.error.message;
      } else if (typeof error.error === 'string') {
        // Server-side error with plain string body
        message = error.error;
      } else {
        // Server-side error with JSON body
        message = error.error?.error
          ?? error.error?.message
          ?? error.message
          ?? `Error ${error.status}`;
      }

      const apiError = new ApiError(message);
      apiError.retryAfterSeconds = error.error?.retryAfterSeconds;
      apiError.statusCode = error.status;
      return throwError(() => apiError);
    }),
  );
};
