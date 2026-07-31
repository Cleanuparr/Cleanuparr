import { HttpEvent, HttpHandlerFn, HttpRequest, HttpResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Observable, of } from 'rxjs';
import { ApplicationPathService } from '@core/services/base-path.service';
import { baseUrlInterceptor } from './base-url.interceptor';

describe('baseUrlInterceptor', () => {
  function setup(basePath: string) {
    TestBed.configureTestingModule({
      providers: [
        { provide: ApplicationPathService, useValue: { getBasePath: () => basePath } },
      ],
    });

    const seen: HttpRequest<unknown>[] = [];
    const next: HttpHandlerFn = (req): Observable<HttpEvent<unknown>> => {
      seen.push(req);
      return of(new HttpResponse<unknown>());
    };

    const run = (url: string): HttpRequest<unknown> => {
      const req = new HttpRequest('GET', url);
      TestBed.runInInjectionContext(() => baseUrlInterceptor(req, next)).subscribe();
      return seen[seen.length - 1];
    };

    return { run, seen };
  }

  it('leaves urls untouched when the base path is the root', () => {
    const { run } = setup('/');

    expect(run('/api/auth/status').url).toBe('/api/auth/status');
    expect(run('/hubs/events').url).toBe('/hubs/events');
  });

  it('prefixes api and hub urls with a sub-path base', () => {
    const { run } = setup('/cleanuparr');

    expect(run('/api/auth/status').url).toBe('/cleanuparr/api/auth/status');
    expect(run('/hubs/events').url).toBe('/cleanuparr/hubs/events');
  });

  it('forwards a url that is neither an api nor a hub url unprefixed', () => {
    const { run } = setup('/cleanuparr');

    const forwarded = run('/assets/icon.svg');

    expect(forwarded.url).toBe('/assets/icon.svg');
  });
});
