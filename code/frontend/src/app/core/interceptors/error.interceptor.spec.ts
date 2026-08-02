import { HttpErrorResponse, HttpRequest, HttpResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { ApiError, errorInterceptor } from './error.interceptor';

function intercept(response: HttpErrorResponse): ApiError {
  const request = new HttpRequest('GET', '/api/thing');
  let caught: ApiError | undefined;

  errorInterceptor(request, () => throwError(() => response) as Observable<HttpResponse<unknown>>).subscribe({
    error: (error: ApiError) => (caught = error),
  });

  if (!caught) {
    throw new Error('interceptor did not surface an error');
  }
  return caught;
}

describe('errorInterceptor', () => {
  it('reports a transport failure when no response reached the client', () => {
    expect(intercept(new HttpErrorResponse({ status: 0 })).message).toBe(
      'Unable to reach the server',
    );
    expect(
      intercept(new HttpErrorResponse({ status: 500, error: new ProgressEvent('error') }))
        .message,
    ).toBe('Unable to reach the server');
  });

  it('surfaces the message of a client-side ErrorEvent', () => {
    const error = intercept(
      new HttpErrorResponse({ status: 500, error: new ErrorEvent('fail', { message: 'boom' }) }),
    );

    expect(error.message).toBe('boom');
  });

  it('passes a non-empty string body through and falls back on an empty one', () => {
    expect(intercept(new HttpErrorResponse({ status: 400, error: 'bad input' })).message).toBe(
      'bad input',
    );
    expect(intercept(new HttpErrorResponse({ status: 400, error: '' })).message).toBe('Error 400');
  });

  it('prefers detail over title over error over message', () => {
    const body = {
      detail: 'the detail',
      title: 'the title',
      error: 'the error',
      message: 'the message',
    };

    expect(intercept(new HttpErrorResponse({ status: 400, error: body })).message).toBe(
      'the detail',
    );
    expect(
      intercept(new HttpErrorResponse({ status: 400, error: { ...body, detail: undefined } }))
        .message,
    ).toBe('the title');
    expect(
      intercept(
        new HttpErrorResponse({
          status: 400,
          error: { ...body, detail: undefined, title: undefined },
        }),
      ).message,
    ).toBe('the error');
    expect(
      intercept(new HttpErrorResponse({ status: 400, error: { message: 'the message' } })).message,
    ).toBe('the message');
    expect(intercept(new HttpErrorResponse({ status: 418, error: {} })).message).toBe('Error 418');
  });

  it('copies problem details metadata from a structured body', () => {
    const error = intercept(
      new HttpErrorResponse({
        status: 429,
        error: { detail: 'slow down', traceId: 'trace-1', retryAfterSeconds: 30 },
      }),
    );

    expect(error.statusCode).toBe(429);
    expect(error.traceId).toBe('trace-1');
    expect(error.retryAfterSeconds).toBe(30);
  });

  it('does not read metadata off event or string bodies', () => {
    const fromEvent = intercept(
      new HttpErrorResponse({ status: 500, error: new ProgressEvent('error') }),
    );
    const fromString = intercept(new HttpErrorResponse({ status: 500, error: 'nope' }));

    expect(fromEvent.traceId).toBeUndefined();
    expect(fromEvent.retryAfterSeconds).toBeUndefined();
    expect(fromEvent.statusCode).toBe(500);
    expect(fromString.traceId).toBeUndefined();
    expect(fromString.retryAfterSeconds).toBeUndefined();
  });
});
