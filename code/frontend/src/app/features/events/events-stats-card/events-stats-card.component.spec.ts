vi.mock('@unovis/angular', async () => (await import('../../../../testing/unovis.stub')).createUnovisStub());

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { EventsApi } from '@core/api/events.api';
import { EventTypeTimelineResponse } from '@core/models/event.models';
import { ApiError } from '@core/interceptors/error.interceptor';
import { EventsStatsCardComponent } from './events-stats-card.component';

const TIMELINE: EventTypeTimelineResponse = {
  types: ['StalledStrike', 'QueueItemDeleted', 'SearchTriggered'],
  buckets: [
    { date: '2026-07-30T10:00:00Z', counts: { StalledStrike: 1, QueueItemDeleted: 3 } },
    { date: '2026-07-30T11:00:00Z', counts: { StalledStrike: 2, QueueItemDeleted: 5, SearchTriggered: 4 } },
  ],
};

const TIED_TIMELINE: EventTypeTimelineResponse = {
  types: ['StalledStrike', 'QueueItemDeleted'],
  buckets: [
    { date: '2026-07-30T10:00:00Z', counts: { StalledStrike: 2, QueueItemDeleted: 1 } },
    { date: '2026-07-30T11:00:00Z', counts: { QueueItemDeleted: 1 } },
  ],
};

const ZEROED_TIMELINE: EventTypeTimelineResponse = {
  types: ['StalledStrike'],
  buckets: [
    { date: '2026-07-30T10:00:00Z', counts: { StalledStrike: 0 } },
    { date: '2026-07-30T11:00:00Z', counts: {} },
  ],
};

describe('EventsStatsCardComponent', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  function setup(options: { timeline?: EventTypeTimelineResponse; timelineError?: boolean } = {}): ComponentFixture<EventsStatsCardComponent> {
    vi.stubGlobal('matchMedia', () => ({ matches: false }));

    TestBed.configureTestingModule({
      providers: [
        {
          provide: EventsApi,
          useValue: { getEventTypeTimeline: () => (options.timelineError ? throwError(() => new ApiError('timeline error')) : of(options.timeline ?? TIMELINE)) },
        },
      ],
    });

    TestBed.overrideComponent(EventsStatsCardComponent, { set: { template: '', imports: [] } });

    const fixture = TestBed.createComponent(EventsStatsCardComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('selects the type with the highest total across every bucket', () => {
    const fixture = setup();

    expect(fixture.componentInstance.current()).toBe('QueueItemDeleted');
  });

  it('resolves a tie to the first type in the declared order', () => {
    const fixture = setup({ timeline: TIED_TIMELINE });

    expect(fixture.componentInstance.current()).toBe('StalledStrike');
  });

  it('honours a valid selection and falls back to the busiest type when the selection is stale', () => {
    const fixture = setup();
    const component = fixture.componentInstance;

    component.selected.set('SearchTriggered');
    fixture.detectChanges();
    expect(component.current()).toBe('SearchTriggered');

    component.selected.set('CategoryChanged');
    fixture.detectChanges();
    expect(component.current()).toBe('QueueItemDeleted');

    component.selected.set(null);
    fixture.detectChanges();
    expect(component.current()).toBe('QueueItemDeleted');
  });

  it('reads zero rather than undefined for a bucket that has no entry for the current type', () => {
    const fixture = setup();
    const component = fixture.componentInstance;

    component.selected.set('SearchTriggered');
    fixture.detectChanges();

    const accessor = component.y();
    expect(accessor(component.data()[0])).toBe(0);
    expect(accessor(component.data()[1])).toBe(4);
  });

  it('uses a unit y domain for an all-zero series instead of a degenerate one', () => {
    const fixture = setup({ timeline: ZEROED_TIMELINE });

    expect(fixture.componentInstance.yDomain()).toEqual([0, 1]);
    expect(fixture.componentInstance.yBaseline()).toBe(0);
  });

  it('renders without throwing when timeline load fails and exposes empty arrays', () => {
    const fixture = setup({ timelineError: true });

    expect(fixture.componentInstance.data()).toEqual([]);
    expect(fixture.componentInstance.allTypes()).toEqual([]);
  });
});
