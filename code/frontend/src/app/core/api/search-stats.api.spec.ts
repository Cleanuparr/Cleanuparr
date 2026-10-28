import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Observable, of } from 'rxjs';
import { SeekerContentKind } from '@core/models/search-stats.models';
import { SearchStatsApi } from './search-stats.api';

interface HttpGetOptions {
  params?: HttpParams;
}

type HttpGetStub = (url: string, options?: HttpGetOptions) => Observable<null>;

describe('SearchStatsApi', () => {
  function setup() {
    const get = vi.fn<HttpGetStub>(() => of(null));
    TestBed.configureTestingModule({
      providers: [{ provide: HttpClient, useValue: { get } }],
    });
    return { api: TestBed.inject(SearchStatsApi), get };
  }

  function sentParams(get: ReturnType<typeof setup>['get']): HttpParams {
    const options = get.mock.calls[0][1];
    return options?.params ?? new HttpParams();
  }

  it('sends the first page of 50 when called without a query', () => {
    const { api, get } = setup();

    api.getEvents();

    expect(get).toHaveBeenCalledWith('/api/seeker/search-stats/events', { params: expect.anything() });
    expect(sentParams(get).toString()).toBe('page=1&pageSize=50');
  });

  it('sends the content kind filter', () => {
    const { api, get } = setup();

    api.getEvents({ contentKind: SeekerContentKind.Anime });

    expect(sentParams(get).get('contentKind')).toBe(SeekerContentKind.Anime);
  });

  it('omits the content kind filter when unset', () => {
    const { api, get } = setup();

    api.getEvents({});

    expect(sentParams(get).has('contentKind')).toBe(false);
  });

  it('posts the cycle reset for the instance', () => {
    const post = vi.fn(() => of(null));
    TestBed.configureTestingModule({
      providers: [{ provide: HttpClient, useValue: { post } }],
    });

    TestBed.inject(SearchStatsApi).resetCycle('instance-1');

    expect(post).toHaveBeenCalledWith('/api/seeker/cycles/instance-1/reset', null);
  });
});
