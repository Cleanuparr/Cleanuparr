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

import { WritableSignal, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AppHubService } from '@core/realtime/app-hub.service';
import { EventsApi } from '@core/api/events.api';
import { JobsApi } from '@core/api/jobs.api';
import { GeneralConfigApi } from '@core/api/general-config.api';
import { CfScoreApi, CfScoreStats } from '@core/api/cf-score.api';
import { StatsApi } from '@core/api/stats.api';
import { ConfirmService } from '@core/services/confirm.service';
import { ManualEvent, ManualEventFilter } from '@core/models/event.models';
import { StatsV2Response } from '@core/models/stats.models';
import { GeneralConfig } from '@shared/models/general-config.model';
import { DashboardComponent } from './dashboard.component';

const ROW_ORDER_KEY = 'dashboard-row-order';
const DEFAULT_ROW_ORDER = ['strikes', 'logs-events', 'cf-scores', 'jobs'];

const STATS = {
  events: { total: 0, byType: {}, bySeverity: {} },
  strikes: { total: 0, byType: {}, recovered: 0 },
  removals: { total: 0, byReason: {} },
  cleaned: { total: 0, byReason: {} },
  searches: { total: 0, completed: 0, failed: 0, grabbed: 0, byReason: {} },
  jobs: { total: 0, completed: 0, failed: 0, byType: {} },
  timeframeHours: 24,
  generatedAt: '2026-07-30T12:00:00Z',
} as StatsV2Response;

const CF_STATS: CfScoreStats = {
  totalTracked: 4,
  belowCutoff: 1,
  atOrAboveCutoff: 3,
  monitored: 4,
  unmonitored: 0,
  recentUpgrades: 2,
  perInstanceStats: [],
};

function manualEvent(id: string, minutesAgo: number, overrides: Partial<ManualEvent> = {}): ManualEvent {
  return {
    id,
    timestamp: new Date(Date.parse('2026-07-30T12:00:00Z') - minutesAgo * 60_000),
    message: `message ${id}`,
    severity: 'Important',
    isResolved: false,
    isDryRun: false,
    ...overrides,
  };
}

function manualEventPage(prefix: string, count: number, startMinutesAgo = 0): ManualEvent[] {
  return Array.from({ length: count }, (_, i) => manualEvent(`${prefix}-${i}`, startMinutesAgo + i));
}

interface StubPage {
  items: ManualEvent[];
  totalCount: number;
}

interface Harness {
  fixture: ComponentFixture<DashboardComponent>;
  hubManualEvents: WritableSignal<ManualEvent[]>;
  requests: ManualEventFilter[];
  resolved: string[];
  resolveAllCalls: () => number;
  confirmState: { answer: boolean; asked: number };
}

describe('DashboardComponent', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.useRealTimers();
    localStorage.clear();
  });

  function setup(options: {
    hubEvents?: ManualEvent[];
    pages?: StubPage[];
    storedOrder?: string;
    cfStats?: CfScoreStats | null;
  } = {}): Harness {
    localStorage.clear();
    if (options.storedOrder !== undefined) {
      localStorage.setItem(ROW_ORDER_KEY, options.storedOrder);
    }
    vi.stubGlobal('matchMedia', () => ({ matches: false }));

    const hubManualEvents = signal<ManualEvent[]>(options.hubEvents ?? []);
    const requests: ManualEventFilter[] = [];
    const resolved: string[] = [];
    const pages = [...(options.pages ?? [{ items: [], totalCount: 0 }])];
    const confirmState = { answer: true, asked: 0 };
    let resolveAll = 0;

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: AppHubService,
          useValue: {
            isConnected: signal(true),
            jobs: signal([]),
            logs: signal([]),
            events: signal([]),
            strikes: signal([]),
            manualEvents: hubManualEvents,
            removeManualEvent: (id: string) => {
              hubManualEvents.update((events) => events.filter((e) => e.id !== id));
            },
            clearManualEvents: () => hubManualEvents.set([]),
          },
        },
        {
          provide: EventsApi,
          useValue: {
            getManualEvents: (filter: ManualEventFilter) => {
              requests.push(filter);
              const page = pages.shift() ?? { items: [], totalCount: 0 };
              return of({
                items: page.items,
                page: 1,
                pageSize: 20,
                totalCount: page.totalCount,
                totalPages: 1,
              });
            },
            resolveManualEvent: (id: string) => {
              resolved.push(id);
              return of(undefined);
            },
            resolveAllManualEvents: () => {
              resolveAll += 1;
              return of({ resolvedCount: 12 });
            },
          },
        },
        { provide: JobsApi, useValue: { trigger: () => of({ message: 'ok' }) } },
        {
          provide: GeneralConfigApi,
          useValue: { get: () => of({ displaySupportBanner: false } as GeneralConfig) },
        },
        {
          provide: CfScoreApi,
          useValue: {
            getStats: () => of(options.cfStats ?? null),
            getRecentUpgrades: () => of({ items: [], page: 1, pageSize: 5, totalCount: 0, totalPages: 0 }),
          },
        },
        {
          provide: StatsApi,
          useValue: { getStats: () => of(STATS), getTimeline: () => of([]) },
        },
        {
          provide: ConfirmService,
          useValue: {
            confirm: () => {
              confirmState.asked += 1;
              return Promise.resolve(confirmState.answer);
            },
          },
        },
      ],
    });

    const fixture = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges();
    return { fixture, hubManualEvents, requests, resolved, resolveAllCalls: () => resolveAll, confirmState };
  }

  function eventIds(fixture: ComponentFixture<DashboardComponent>): string[] {
    return fixture.componentInstance.unresolvedManualEvents().map((e) => e.id);
  }

  it('counts the larger of the server total and the merged list, and shows the position in the banner', () => {
    const { fixture, hubManualEvents } = setup({
      pages: [{ items: manualEventPage('backlog', 2, 10), totalCount: 7 }],
    });
    const component = fixture.componentInstance;

    expect(eventIds(fixture)).toEqual(['backlog-0', 'backlog-1']);
    expect(component.manualEventCount()).toBe(7);
    expect(
      (fixture.nativeElement.querySelector('.manual-event__counter') as HTMLElement).textContent!.trim(),
    ).toBe('1 of 7');

    hubManualEvents.set(manualEventPage('live', 8));
    fixture.detectChanges();

    expect(component.unresolvedManualEvents()).toHaveLength(10);
    expect(component.manualEventCount()).toBe(10);
  });

  it('walks the loaded events with prev and next and clamps at both ends', () => {
    const { fixture } = setup({
      pages: [{ items: manualEventPage('backlog', 3), totalCount: 3 }],
    });
    const component = fixture.componentInstance;

    expect(component.currentManualEvent()!.id).toBe('backlog-0');
    expect(component.canNavigatePrev()).toBe(false);
    expect(component.hasMoreBacklog()).toBe(false);

    component.prevManualEvent();
    expect(component.manualEventIndex()).toBe(0);

    component.nextManualEvent();
    component.nextManualEvent();
    fixture.detectChanges();
    expect(component.currentManualEvent()!.id).toBe('backlog-2');
    expect(component.canNavigateNext()).toBe(false);

    component.nextManualEvent();
    expect(component.manualEventIndex()).toBe(2);

    component.prevManualEvent();
    component.prevManualEvent();
    component.prevManualEvent();
    fixture.detectChanges();
    expect(component.manualEventIndex()).toBe(0);
    expect(component.currentManualEvent()!.id).toBe('backlog-0');
  });

  it('keeps next available while backlog remains and fetches the next slice from the oldest cursor', () => {
    const firstPage = manualEventPage('p1', 20);
    const { fixture, requests } = setup({
      pages: [
        { items: firstPage, totalCount: 25 },
        { items: manualEventPage('p2', 5, 20), totalCount: 25 },
      ],
    });
    const component = fixture.componentInstance;

    expect(requests).toEqual([{ pageSize: 20, isResolved: false, toDate: undefined }]);
    expect(component.hasMoreBacklog()).toBe(true);

    component.manualEventIndex.set(19);
    fixture.detectChanges();
    expect(component.canNavigateNext()).toBe(true);

    component.nextManualEvent();
    fixture.detectChanges();

    expect(requests[1]).toEqual({
      pageSize: 20,
      isResolved: false,
      toDate: String(firstPage[19].timestamp),
    });
    expect(component.unresolvedManualEvents()).toHaveLength(25);
    expect(component.manualEventIndex()).toBe(20);
    expect(component.hasMoreBacklog()).toBe(false);

    component.manualEventIndex.set(24);
    fixture.detectChanges();
    expect(component.canNavigateNext()).toBe(false);
  });

  it('drops a dismissed event from the hub and the backlog and lowers the count', () => {
    const { fixture, hubManualEvents, resolved } = setup({
      hubEvents: [manualEvent('live-0', 0)],
      pages: [{ items: manualEventPage('backlog', 2, 10), totalCount: 5 }],
    });
    const component = fixture.componentInstance;

    expect(eventIds(fixture)).toEqual(['live-0', 'backlog-0', 'backlog-1']);

    component.dismissManualEvent(component.currentManualEvent()!);
    fixture.detectChanges();

    expect(resolved).toEqual(['live-0']);
    expect(hubManualEvents()).toEqual([]);
    expect(eventIds(fixture)).toEqual(['backlog-0', 'backlog-1']);
    expect(component.manualEventCount()).toBe(4);

    component.dismissManualEvent(component.currentManualEvent()!);
    fixture.detectChanges();

    expect(resolved).toEqual(['live-0', 'backlog-0']);
    expect(eventIds(fixture)).toEqual(['backlog-1']);
    expect(component.manualEventCount()).toBe(3);
  });

  it('tops up from the next slice only once dismissing takes the buffer to the refill threshold', () => {
    const { fixture, requests } = setup({
      pages: [
        { items: manualEventPage('p1', 20), totalCount: 30 },
        { items: manualEventPage('p2', 4, 20), totalCount: 30 },
      ],
    });
    const component = fixture.componentInstance;

    for (let i = 0; i < 16; i++) {
      component.dismissManualEvent(component.unresolvedManualEvents()[0]);
    }
    fixture.detectChanges();

    expect(component.unresolvedManualEvents()).toHaveLength(4);
    expect(requests).toHaveLength(1);

    component.dismissManualEvent(component.unresolvedManualEvents()[0]);
    fixture.detectChanges();

    expect(requests).toHaveLength(2);
    expect(eventIds(fixture)).toEqual(['p1-17', 'p1-18', 'p1-19', 'p2-0', 'p2-1', 'p2-2', 'p2-3']);
  });

  it('clears every source when dismissing all is confirmed and does nothing when it is not', async () => {
    const { fixture, hubManualEvents, confirmState, resolveAllCalls } = setup({
      hubEvents: [manualEvent('live-0', 0)],
      pages: [{ items: manualEventPage('backlog', 20, 10), totalCount: 30 }],
    });
    const component = fixture.componentInstance;
    component.manualEventIndex.set(5);

    confirmState.answer = false;
    await component.dismissAllManualEvents();
    fixture.detectChanges();

    expect(confirmState.asked).toBe(1);
    expect(resolveAllCalls()).toBe(0);
    expect(component.unresolvedManualEvents()).toHaveLength(21);

    confirmState.answer = true;
    await component.dismissAllManualEvents();
    fixture.detectChanges();

    expect(resolveAllCalls()).toBe(1);
    expect(hubManualEvents()).toEqual([]);
    expect(component.unresolvedManualEvents()).toEqual([]);
    expect(component.manualEventCount()).toBe(0);
    expect(component.manualEventIndex()).toBe(0);
    expect(component.hasMoreBacklog()).toBe(true);
    expect(component.currentManualEvent()).toBeNull();
    expect(fixture.nativeElement.querySelector('.manual-event')).toBeNull();
  });

  it('falls back to the default row order when the stored order is unusable and hides the cf score row', () => {
    const { fixture } = setup({ storedOrder: 'not json' });

    expect(fixture.componentInstance.rowOrder()).toEqual(DEFAULT_ROW_ORDER);
    expect(fixture.componentInstance.visibleRowOrder()).toEqual(['strikes', 'logs-events', 'jobs']);
    expect(fixture.nativeElement.querySelectorAll('.dashboard-row')).toHaveLength(3);
  });

  it('keeps a partial stored order, drops unknown ids and appends the missing rows', () => {
    const { fixture } = setup({
      storedOrder: JSON.stringify(['jobs', 'bogus', 'logs-events']),
      cfStats: CF_STATS,
    });

    expect(fixture.componentInstance.rowOrder()).toEqual(['jobs', 'logs-events', 'strikes', 'cf-scores']);
    expect(fixture.componentInstance.visibleRowOrder()).toEqual(['jobs', 'logs-events', 'strikes', 'cf-scores']);
  });

  it('persists the reordered rows and keeps the hidden ones at the end', () => {
    const { fixture } = setup();
    const component = fixture.componentInstance;

    component.onDrop({ previousIndex: 0, currentIndex: 2 } as Parameters<DashboardComponent['onDrop']>[0]);
    fixture.detectChanges();

    expect(component.rowOrder()).toEqual(['logs-events', 'jobs', 'strikes', 'cf-scores']);
    expect(JSON.parse(localStorage.getItem(ROW_ORDER_KEY)!)).toEqual([
      'logs-events',
      'jobs',
      'strikes',
      'cf-scores',
    ]);
  });
});
