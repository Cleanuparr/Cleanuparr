import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Observable, of } from 'rxjs';
import { EventsApi } from './events.api';

interface HttpGetOptions {
  params?: HttpParams;
}

type HttpGetStub = (url: string, options?: HttpGetOptions) => Observable<null>;

describe('EventsApi', () => {
  function setup() {
    const get = vi.fn<HttpGetStub>(() => of(null));
    TestBed.configureTestingModule({
      providers: [{ provide: HttpClient, useValue: { get } }],
    });
    return { api: TestBed.inject(EventsApi), get };
  }

  function sentParams(get: ReturnType<typeof setup>['get']): HttpParams {
    const options = get.mock.calls[0][1];
    return options?.params ?? new HttpParams();
  }

  it('sends no query parameters when called without a filter', () => {
    const { api, get } = setup();

    api.getEvents();

    expect(get).toHaveBeenCalledWith('/api/events', { params: expect.anything() });
    expect(sentParams(get).toString()).toBe('');
  });

  it('drops falsy filter fields so page zero and an empty search never reach the server', () => {
    const { api, get } = setup();

    api.getEvents({ page: 0, pageSize: 25, search: '', severity: 'error' });

    const params = sentParams(get);
    expect(params.has('page')).toBe(false);
    expect(params.has('search')).toBe(false);
    expect(params.get('pageSize')).toBe('25');
    expect(params.get('severity')).toBe('error');
  });

  it('sends a false boolean filter field that is checked against undefined', () => {
    const { api, get } = setup();

    api.getManualEvents({ isResolved: false });

    expect(sentParams(get).get('isResolved')).toBe('false');
  });

  it('sends the requested window on the event type timeline call', () => {
    const { api, get } = setup();

    api.getEventTypeTimeline(24);

    expect(sentParams(get).get('hours')).toBe('24');
  });
});
