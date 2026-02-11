import { HttpInterceptorFn } from '@angular/common/http';

// Placeholder: no-op interceptor. When auth is implemented, this will
// attach JWT/session tokens to outgoing requests and handle 401 responses.
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  return next(req);
};
