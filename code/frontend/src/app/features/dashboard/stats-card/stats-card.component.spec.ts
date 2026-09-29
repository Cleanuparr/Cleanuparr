vi.mock('@unovis/angular', async () => (await import('../../../../testing/unovis.stub')).createUnovisStub());

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { StatsApi } from '@core/api/stats.api';
import { StatsV2Response } from '@core/models/stats.models';
import { ApiError } from '@core/interceptors/error.interceptor';
import { StatsCardComponent } from './stats-card.component';

const STATS: StatsV2Response = {
  events: { total: 0, byType: {}, bySeverity: {} },
  strikes: { total: 0, byType: {}, recovered: 0 },
  removals: { total: 0, byReason: {} },
  cleaned: { total: 0, byReason: {} },
  searches: { total: 0, completed: 0, failed: 0, grabbed: 0, byReason: {} },
  jobs: { total: 0, completed: 0, failed: 0, byType: {} },
  timeframeHours: 24,
  generatedAt: '2026-07-30T12:00:00Z',
};

const TIMELINE = [
  { date: '2026-07-30T10:00:00Z', count: 5 },
  { date: '2026-07-30T11:00:00Z', count: 3 },
];

describe('StatsCardComponent', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  function setup(options: { statsError?: boolean; timelineError?: boolean } = {}): ComponentFixture<StatsCardComponent> {
    vi.stubGlobal('matchMedia', () => ({ matches: false }));

    TestBed.configureTestingModule({
      providers: [
        {
          provide: StatsApi,
          useValue: {
            getStats: () => (options.statsError ? throwError(() => new ApiError('stats error')) : of(STATS)),
            getTimeline: () => (options.timelineError ? throwError(() => new ApiError('timeline error')) : of(TIMELINE)),
          },
        },
      ],
    });

    const fixture = TestBed.createComponent(StatsCardComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('renders without throwing when stats load fails and exposes empty stats', () => {
    const fixture = setup({ statsError: true });

    const tiles = fixture.componentInstance.tiles();
    expect(tiles).toHaveLength(5);
    expect(tiles[0].key).toBe('removed');
    expect(tiles[0].value).toBe(0);
    expect(tiles[1].key).toBe('recovered');
    expect(tiles[1].value).toBe(0);
    expect(tiles[2].key).toBe('issued');
    expect(tiles[2].value).toBe(0);
    expect(tiles[3].key).toBe('malware');
    expect(tiles[3].value).toBe(0);
    expect(tiles[4].key).toBe('jobs');
    expect(tiles[4].value).toBe(0);
    expect(fixture.nativeElement).toBeDefined();
  });

  it('renders without throwing when timeline load fails and exposes empty array', () => {
    const fixture = setup({ timelineError: true });

    expect(fixture.componentInstance.timeline()).toEqual([]);
    expect(fixture.nativeElement).toBeDefined();
  });
});
