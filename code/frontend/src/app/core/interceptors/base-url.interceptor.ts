import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { ApplicationPathService } from '@core/services/base-path.service';

export const baseUrlInterceptor: HttpInterceptorFn = (req, next) => {
  if (req.url.startsWith('/api') || req.url.startsWith('/hubs')) {
    const pathService = inject(ApplicationPathService);
    const basePath = pathService.getBasePath();

    // In dev mode basePath is a full URL (http://localhost:5000)
    // In production it's a path prefix (e.g. '/' or '/cleanuparr')
    const url = basePath === '/'
      ? req.url
      : basePath + req.url;

    return next(req.clone({ url }));
  }
  return next(req);
};
