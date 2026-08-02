import { TestBed } from '@angular/core/testing';
import { FeatureViewsApi, FeatureViewsResponse } from '@core/api/feature-views.api';
import { Observable, of, throwError } from 'rxjs';
import { FeatureBadgeService } from './feature-badge.service';
import { DEFAULT_NEW_BADGE_DURATION_DAYS, NEW_FEATURES } from './feature-registry';

const DAY_MS = 24 * 60 * 60 * 1000;
const HOUR_MS = 60 * 60 * 1000;
const NOW = new Date('2026-01-15T12:00:00.000Z').getTime();
const FEATURE_ID = NEW_FEATURES[0].id;
const ANCHOR_KEY = 'cleanuparr-feature-anchor';
const VIEWS_KEY = 'cleanuparr-feature-views';

describe('FeatureBadgeService', () => {
  function setup(record: () => Observable<FeatureViewsResponse>): FeatureBadgeService {
    TestBed.configureTestingModule({
      providers: [{ provide: FeatureViewsApi, useValue: { record } }],
    });
    const service = TestBed.inject(FeatureBadgeService);
    service.init();
    return service;
  }

  function serverViews(createdAt: number, firstSeen: number): () => Observable<FeatureViewsResponse> {
    return () =>
      of({
        createdAt: new Date(createdAt).toISOString(),
        views: { [FEATURE_ID]: new Date(firstSeen).toISOString() },
      });
  }

  function storedViews(): Record<string, number> {
    return JSON.parse(localStorage.getItem(VIEWS_KEY) ?? '{}');
  }

  beforeEach(() => {
    localStorage.clear();
    vi.useFakeTimers();
    vi.setSystemTime(NOW);
  });

  afterEach(() => {
    vi.useRealTimers();
    localStorage.clear();
  });

  it('reports a feature first seen well after the account was created as new', () => {
    const service = setup(serverViews(NOW - 30 * DAY_MS, NOW - 2 * DAY_MS));

    expect(service.isNew(FEATURE_ID)).toBe(true);
  });

  it('does not report a feature first seen inside the anchor grace period as new', () => {
    const createdAt = NOW - 30 * DAY_MS;

    const service = setup(serverViews(createdAt, createdAt + HOUR_MS));

    expect(service.isNew(FEATURE_ID)).toBe(false);
  });

  it('does not report a feature first seen exactly at the end of the grace period as new', () => {
    const createdAt = NOW - 30 * DAY_MS;

    const service = setup(serverViews(createdAt, createdAt + DAY_MS));

    expect(service.isNew(FEATURE_ID)).toBe(false);
  });

  it('stops reporting the feature as new once the show duration has fully elapsed', () => {
    const duration = DEFAULT_NEW_BADGE_DURATION_DAYS * DAY_MS;
    const firstSeen = NOW - duration + 1;
    const service = setup(serverViews(NOW - 90 * DAY_MS, firstSeen));

    expect(service.isNew(FEATURE_ID)).toBe(true);

    vi.setSystemTime(firstSeen + duration);

    expect(service.isNew(FEATURE_ID)).toBe(false);
  });

  it('reports an unknown feature id and a registered id without a recorded timestamp as not new', () => {
    const service = setup(() =>
      of({ createdAt: new Date(NOW - 30 * DAY_MS).toISOString(), views: {} }),
    );

    expect(service.isNew('not-a-registered-feature')).toBe(false);
    expect(service.isNew(FEATURE_ID)).toBe(false);
  });

  it('records the anchor and first seen timestamps locally when the api call fails', () => {
    const service = setup(() => throwError(() => new Error('offline')));

    expect(localStorage.getItem(ANCHOR_KEY)).toBe(String(NOW));
    expect(storedViews()[FEATURE_ID]).toBe(NOW);
    expect(service.isNew(FEATURE_ID)).toBe(false);
  });

  it('reuses the stored anchor and first seen timestamps on a later local initialisation', () => {
    const anchor = NOW - 30 * DAY_MS;
    localStorage.setItem(ANCHOR_KEY, String(anchor));

    const service = setup(() => throwError(() => new Error('offline')));

    expect(service.isNew(FEATURE_ID)).toBe(true);

    TestBed.resetTestingModule();
    vi.setSystemTime(NOW + 3 * DAY_MS);
    const reloaded = setup(() => throwError(() => new Error('offline')));

    expect(localStorage.getItem(ANCHOR_KEY)).toBe(String(anchor));
    expect(storedViews()[FEATURE_ID]).toBe(NOW);
    expect(reloaded.isNew(FEATURE_ID)).toBe(true);
  });
});
