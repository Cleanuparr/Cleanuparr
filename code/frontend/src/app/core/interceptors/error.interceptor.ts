import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';

interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  traceId?: string;
  retryAfterSeconds?: number;
}

export class ApiError extends Error {
  retryAfterSeconds?: number;
  statusCode?: number;
  traceId?: string;
}

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.error instanceof ErrorEvent) {
        // Client-side / network error
        const apiError = new ApiError(error.error.message);
        apiError.statusCode = error.status;
        return throwError(() => apiError);
      }

      const problem = error.error as ProblemDetails | null;
      const apiError = new ApiError(problem?.detail ?? problem?.title ?? `Error ${error.status}`);
      apiError.statusCode = error.status;
      apiError.retryAfterSeconds = problem?.retryAfterSeconds;
      apiError.traceId = problem?.traceId;
      return throwError(() => apiError);
    }),
  );
};
