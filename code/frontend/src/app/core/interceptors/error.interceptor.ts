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

function resolveMessage(error: HttpErrorResponse): string {
  // Transport-level failure: no HTTP response reached the client (offline, CORS, DNS, ...).
  if (error.status === 0 || error.error instanceof ProgressEvent) {
    return 'Unable to reach the server';
  }

  // Client-side error raised while processing the request.
  if (error.error instanceof ErrorEvent) {
    return error.error.message;
  }

  // Plain-string body.
  if (typeof error.error === 'string' && error.error.length > 0) {
    return error.error;
  }

  // Structured body: prefer RFC 9457 ProblemDetails, then fall back to legacy { error } / { message } shapes.
  const body = error.error as (ProblemDetails & { error?: string; message?: string }) | null;
  return body?.detail ?? body?.title ?? body?.error ?? body?.message ?? `Error ${error.status}`;
}

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      const apiError = new ApiError(resolveMessage(error));
      apiError.statusCode = error.status;

      const body = error.error;
      if (body && typeof body === 'object' && !(body instanceof ErrorEvent) && !(body instanceof ProgressEvent)) {
        const problem = body as ProblemDetails;
        apiError.retryAfterSeconds = problem.retryAfterSeconds;
        apiError.traceId = problem.traceId;
      }

      return throwError(() => apiError);
    }),
  );
};
