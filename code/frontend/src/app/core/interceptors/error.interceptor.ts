import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      let message = 'An unexpected error occurred';

      if (error.error instanceof ErrorEvent) {
        // Client-side error
        message = error.error.message;
      } else {
        // Server-side error
        message = error.error?.error
          ?? error.error?.message
          ?? error.message
          ?? `Error ${error.status}`;
      }

      return throwError(() => new Error(message));
    }),
  );
};
