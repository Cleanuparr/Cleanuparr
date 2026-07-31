vi.mock('@unovis/angular', () => {
  const stubs = new Map<string, unknown>();
  return new Proxy({} as Record<string, unknown>, {
    has: () => true,
    get: (_target, property) => {
      if (typeof property !== 'string' || property === 'then') {
        return undefined;
      }
      if (!stubs.has(property)) {
        stubs.set(property, class {});
      }
      return stubs.get(property);
    },
  });
});

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { EventsApi } from '@core/api/events.api';
import { AppEvent, EventFilter, EventTypeTimelineResponse } from '@core/models/event.models';
import { PaginatedResult } from '@core/models/pagination.model';
import { EventsComponent } from './events.component';

const PAGE_SIZE_KEY = 'cleanuparr-page-size-events';

class IntersectionObserverStub {
  observe(): void {
    return undefined;
  }

  unobserve(): void {
    return undefined;
  }

  disconnect(): void {
    return undefined;
  }
}

const EMPTY_TIMELINE: EventTypeTimelineResponse = { types: [], buckets: [] };

function appEvent(overrides: Partial<AppEvent> & { id: string }): AppEvent {
  return {
    timestamp: new Date('2026-07-30T12:00:00Z'),
    eventType: 'StalledStrike',
    message: 'Download stalled',
    severity: 'Warning',
    isDryRun: false,
    ...overrides,
  };
}

const EVENTS: AppEvent[] = [
  appEvent({
    id: 'event-1',
    itemTitle: 'Some "quoted" release',
    strikeCount: 2,
    failedImportReasons: ['no files', 'unpack failed'],
    jobRunId: 'run-a',
  }),
  appEvent({
    id: 'event-2',
    eventType: 'QueueItemDeleted',
    severity: 'Error',
    message: 'Removed from queue',
  }),
];

interface Harness {
  fixture: ComponentFixture<EventsComponent>;
  filters: EventFilter[];
}

describe('EventsComponent', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.useRealTimers();
    vi.restoreAllMocks();
    localStorage.clear();
  });

  function setup(options: { storedPageSize?: string; totalCount?: number } = {}): Harness {
    localStorage.clear();
    if (options.storedPageSize !== undefined) {
      localStorage.setItem(PAGE_SIZE_KEY, options.storedPageSize);
    }
    vi.stubGlobal('IntersectionObserver', IntersectionObserverStub);
    vi.stubGlobal('matchMedia', () => ({ matches: false }));

    const filters: EventFilter[] = [];

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: EventsApi,
          useValue: {
            getEvents: (filter: EventFilter) => {
              filters.push(filter);
              return of({
                items: EVENTS,
                page: filter.page ?? 1,
                pageSize: filter.pageSize ?? 50,
                totalCount: options.totalCount ?? EVENTS.length,
                totalPages: 1,
              } as PaginatedResult<AppEvent>);
            },
            getSeverities: () => of(['Information', 'Warning', 'Error']),
            getEventTypes: () => of(['StalledStrike', 'QueueItemDeleted']),
            getEventTypeTimeline: () => of(EMPTY_TIMELINE),
          },
        },
      ],
    });

    const fixture = TestBed.createComponent(EventsComponent);
    fixture.detectChanges();
    return { fixture, filters };
  }

  function lastFilter(filters: EventFilter[]): EventFilter {
    return filters[filters.length - 1];
  }

  it('sends only the populated filters to the api', () => {
    const { fixture, filters } = setup();
    const component = fixture.componentInstance;

    expect(filters).toEqual([{ page: 1, pageSize: 50 }]);

    component.selectedSeverity.set('Error');
    component.selectedType.set('QueueItemDeleted');
    component.searchQuery.set('stalled');
    component.fromDate.set('2026-07-01T00:00');
    component.toDate.set('2026-07-31T00:00');
    fixture.detectChanges();

    expect(lastFilter(filters)).toEqual({
      page: 1,
      pageSize: 50,
      severity: 'Error',
      eventType: 'QueueItemDeleted',
      search: 'stalled',
      fromDate: '2026-07-01T00:00',
      toDate: '2026-07-31T00:00',
    });

    component.selectedSeverity.set('');
    component.selectedType.set('');
    component.searchQuery.set('');
    component.fromDate.set('');
    component.toDate.set('');
    fixture.detectChanges();

    expect(lastFilter(filters)).toEqual({ page: 1, pageSize: 50 });
  });

  it('returns to the first page when a filter changes after paging', () => {
    const { fixture, filters } = setup({ totalCount: 500 });
    const component = fixture.componentInstance;

    component.onPageChange(4);
    fixture.detectChanges();
    expect(lastFilter(filters).page).toBe(4);

    component.selectedSeverity.set('Error');
    component.onFilterChange();
    fixture.detectChanges();

    expect(component.currentPage()).toBe(1);
    expect(lastFilter(filters)).toEqual({ page: 1, pageSize: 50, severity: 'Error' });
  });

  it('filters by a job run and drops the filter again, resetting the page each time', () => {
    const { fixture, filters } = setup({ totalCount: 500 });
    const component = fixture.componentInstance;

    component.onPageChange(2);
    fixture.detectChanges();

    component.filterByJobRunId('run-a');
    fixture.detectChanges();

    expect(lastFilter(filters)).toEqual({ page: 1, pageSize: 50, jobRunId: 'run-a' });
    expect(fixture.nativeElement.querySelector('.active-filter')).not.toBeNull();

    component.onPageChange(2);
    fixture.detectChanges();
    component.clearJobRunFilter();
    fixture.detectChanges();

    expect(lastFilter(filters)).toEqual({ page: 1, pageSize: 50 });
    expect(fixture.nativeElement.querySelector('.active-filter')).toBeNull();
  });

  it('persists a valid page size and ignores an invalid one', () => {
    const { fixture, filters } = setup({ totalCount: 500 });
    const component = fixture.componentInstance;

    component.onPageChange(3);
    fixture.detectChanges();

    component.onPageSizeChange(25);
    fixture.detectChanges();

    expect(localStorage.getItem(PAGE_SIZE_KEY)).toBe('25');
    expect(component.currentPage()).toBe(1);
    expect(lastFilter(filters)).toEqual({ page: 1, pageSize: 25 });

    component.onPageSizeChange(-5);
    fixture.detectChanges();
    expect(component.pageSize()).toBe(25);
    expect(localStorage.getItem(PAGE_SIZE_KEY)).toBe('25');
  });

  it('starts from the page size stored for this page', () => {
    const { fixture, filters } = setup({ storedPageSize: '25' });

    expect(fixture.componentInstance.pageSize()).toBe(25);
    expect(filters[0].pageSize).toBe(25);
  });

  it('prepends the all entries to the severity and type options and spaces out the type names', () => {
    const { fixture } = setup();
    const component = fixture.componentInstance;

    expect(component.severityOptions()).toEqual([
      { label: 'All Severities', value: '' },
      { label: 'Information', value: 'Information' },
      { label: 'Warning', value: 'Warning' },
      { label: 'Error', value: 'Error' },
    ]);
    expect(component.typeOptions()).toEqual([
      { label: 'All Types', value: '' },
      { label: 'Stalled Strike', value: 'StalledStrike' },
      { label: 'Queue Item Deleted', value: 'QueueItemDeleted' },
    ]);
  });

  it('builds the event details, skipping absent values, joining lists and caching the result', () => {
    const { fixture } = setup();
    const component = fixture.componentInstance;

    const details = component.eventDetails(EVENTS[0]);
    expect(details).toEqual([
      { label: 'Item', value: 'Some "quoted" release' },
      { label: 'Strike count', value: '2' },
      { label: 'Failed import reasons', value: 'no files, unpack failed' },
    ]);
    expect(component.eventDetails(EVENTS[0])).toBe(details);

    expect(component.eventDetails(EVENTS[1])).toEqual([]);
    expect(component.isExpandable(EVENTS[0])).toBe(true);
    expect(component.isExpandable(EVENTS[1])).toBe(false);
    expect(component.isExpandable(appEvent({ id: 'event-3', instanceType: 'Sonarr' }))).toBe(true);
  });

  it('expands one row at a time and renders its details', () => {
    const { fixture } = setup();
    const component = fixture.componentInstance;

    component.toggleExpand('event-1');
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('.event-row--expanded')).toHaveLength(1);
    const values = Array.from(fixture.nativeElement.querySelectorAll('.event-row__data-value')).map((el) =>
      (el as HTMLElement).textContent!.trim(),
    );
    expect(values).toEqual(['Some "quoted" release', '2', 'no files, unpack failed']);

    component.toggleExpand('event-1');
    fixture.detectChanges();
    expect(component.expandedId()).toBeNull();
    expect(fixture.nativeElement.querySelectorAll('.event-row--expanded')).toHaveLength(0);
  });

  it('exports the loaded events as csv with escaped quotes and closes the export menu', async () => {
    const { fixture } = setup();
    const component = fixture.componentInstance;
    const blobs: Blob[] = [];
    const originalCreate = URL.createObjectURL;
    const originalRevoke = URL.revokeObjectURL;
    URL.createObjectURL = (obj: Blob | MediaSource): string => {
      blobs.push(obj as Blob);
      return 'blob:stub';
    };
    URL.revokeObjectURL = (): void => undefined;
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);

    try {
      component.showExportMenu.set(true);
      component.exportEvents('csv');
      fixture.detectChanges();

      expect(component.showExportMenu()).toBe(false);
      expect(blobs).toHaveLength(1);
      const lines = (await blobs[0].text()).split('\n');
      expect(lines[0].startsWith('Timestamp,Severity,EventType,Message,Item')).toBe(true);
      expect(lines[1]).toContain('"Download stalled","Some ""quoted"" release"');
      expect(lines[2]).toContain('QueueItemDeleted,"Removed from queue",""');
    } finally {
      URL.createObjectURL = originalCreate;
      URL.revokeObjectURL = originalRevoke;
    }
  });

  it('polls the list while alive and stops polling once destroyed', () => {
    vi.useFakeTimers();
    const { fixture, filters } = setup();
    const afterInit = filters.length;

    vi.advanceTimersByTime(10_000);
    fixture.detectChanges();
    expect(filters.length).toBe(afterInit + 1);

    fixture.destroy();
    vi.advanceTimersByTime(30_000);

    expect(filters.length).toBe(afterInit + 1);
  });
});
