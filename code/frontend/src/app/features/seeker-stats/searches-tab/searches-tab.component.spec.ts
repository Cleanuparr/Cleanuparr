import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { SearchStatsApi, SearchEventsSortBy, SortDirection } from '@core/api/search-stats.api';
import type { SearchEventsQuery } from '@core/api/search-stats.api';
import { SeekerSearchType, SeekerSearchReason, SearchCommandStatus } from '@core/models/search-stats.models';
import type { InstanceSearchStat, SearchEvent, SearchStatsSummary } from '@core/models/search-stats.models';
import type { PaginatedResult } from '@core/models/pagination.model';
import { AppHubService } from '@core/realtime/app-hub.service';
import { SearchesTabComponent } from './searches-tab.component';

const PAGE_SIZE_KEY = 'cleanuparr-page-size-seeker-searches';

function instance(partial: Partial<InstanceSearchStat> = {}): InstanceSearchStat {
  return {
    instanceId: 'instance-1',
    instanceName: 'Instance',
    instanceType: 'Sonarr',
    itemsTracked: 10,
    totalSearchCount: 5,
    lastSearchedAt: '2026-07-30T10:00:00Z',
    lastProcessedAt: '2026-07-30T10:00:00Z',
    currentCycleId: 'cycle-aaaaaaaa-1111',
    cycleItemsSearched: 3,
    cycleItemsTotal: 8,
    cycleStartedAt: '2026-07-29T09:00:00Z',
    ...partial,
  };
}

const SONARR_BETA = instance({ instanceId: 'sonarr-beta', instanceName: 'Beta', instanceType: 'Sonarr' });
const RADARR_ZULU = instance({
  instanceId: 'radarr-zulu',
  instanceName: 'Zulu',
  instanceType: 'Radarr',
  currentCycleId: 'cycle-zulu-9999',
});
const RADARR_ALPHA = instance({
  instanceId: 'radarr-alpha',
  instanceName: 'Alpha',
  instanceType: 'Radarr',
  lastSearchedAt: null,
  totalSearchCount: 0,
  currentCycleId: null,
  cycleStartedAt: null,
  cycleItemsSearched: 0,
  cycleItemsTotal: 0,
});

const SUMMARY: SearchStatsSummary = {
  totalSearchesAllTime: 120,
  searchesLast7Days: 12,
  searchesLast30Days: 40,
  uniqueItemsSearched: 77,
  pendingReplacementSearches: 2,
  enabledInstances: 3,
  perInstanceStats: [SONARR_BETA, RADARR_ZULU, RADARR_ALPHA],
};

const EVENTS: PaginatedResult<SearchEvent> = {
  items: [
    {
      id: 'event-1',
      timestamp: '2026-07-30T10:00:00Z',
      arrInstanceId: 'sonarr-beta',
      instanceType: 'Sonarr',
      itemTitle: 'The Show S01E01',
      searchType: SeekerSearchType.Proactive,
      searchReason: SeekerSearchReason.QualityCutoffNotMet,
      searchStatus: 'Completed',
      completedAt: '2026-07-30T10:05:00Z',
      grabbedItems: ['Release.One', 'Release.Two'],
      cycleId: 'cycle-aaaaaaaa-1111',
      isDryRun: false,
    },
    {
      id: 'event-2',
      timestamp: '2026-07-30T09:00:00Z',
      arrInstanceId: 'radarr-zulu',
      instanceType: 'Radarr',
      itemTitle: '',
      searchType: SeekerSearchType.Replacement,
      searchReason: null,
      searchStatus: null,
      completedAt: null,
      grabbedItems: null,
      cycleId: null,
      isDryRun: true,
    },
  ],
  page: 1,
  pageSize: 50,
  totalCount: 2,
  totalPages: 1,
};

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

interface Harness {
  fixture: ComponentFixture<SearchesTabComponent>;
  component: SearchesTabComponent;
  queries: SearchEventsQuery[];
  lastQuery: () => SearchEventsQuery;
}

describe('SearchesTabComponent', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.useRealTimers();
    localStorage.removeItem(PAGE_SIZE_KEY);
  });

  function setup(summary: SearchStatsSummary = SUMMARY): Harness {
    vi.stubGlobal('IntersectionObserver', IntersectionObserverStub);
    const queries: SearchEventsQuery[] = [];

    TestBed.configureTestingModule({
      providers: [
        {
          provide: SearchStatsApi,
          useValue: {
            getSummary: () => of(summary),
            getEvents: (query: SearchEventsQuery) => {
              queries.push(query);
              return of(EVENTS);
            },
          },
        },
        {
          provide: AppHubService,
          useValue: { searchStatsVersion: signal(0) },
        },
      ],
    });

    const fixture = TestBed.createComponent(SearchesTabComponent);
    fixture.detectChanges();

    return {
      fixture,
      component: fixture.componentInstance,
      queries,
      lastQuery: () => queries[queries.length - 1],
    };
  }

  function rowTitles(fixture: ComponentFixture<SearchesTabComponent>): string[] {
    return Array.from(fixture.nativeElement.querySelectorAll('.list-row__title')).map((row) =>
      (row as HTMLElement).textContent!.trim(),
    );
  }

  it('sorts the instance cards by type then name while leaving the filter options in API order', () => {
    const { component } = setup();

    expect(component.sortedInstanceStats().map((stat) => stat.instanceName)).toEqual(['Alpha', 'Zulu', 'Beta']);
    expect(component.instanceOptions()).toEqual([
      { label: 'All Instances', value: '' },
      { label: 'Beta', value: 'sonarr-beta' },
      { label: 'Zulu', value: 'radarr-zulu' },
      { label: 'Alpha', value: 'radarr-alpha' },
    ]);
  });

  it('starts with the default query and no active filters', () => {
    const { component, lastQuery } = setup();

    expect(component.activeFilterCount()).toBe(0);
    expect(lastQuery()).toEqual({
      page: 1,
      pageSize: 50,
      instanceId: undefined,
      cycleId: undefined,
      search: undefined,
      sortBy: SearchEventsSortBy.Timestamp,
      sortDirection: SortDirection.Desc,
      searchStatus: undefined,
      searchType: undefined,
      searchReason: undefined,
      grabbed: undefined,
    });
  });

  it('feeds every applied filter into the events query and counts them', () => {
    const { fixture, component, lastQuery } = setup();

    component.searchQuery.set('show');
    component.openFilters();
    component.updateDraft('instanceId', 'radarr-zulu');
    component.toggleStatus(SearchCommandStatus.Completed);
    component.toggleStatus(SearchCommandStatus.Failed);
    component.toggleStatus(SearchCommandStatus.Completed);
    component.updateDraft('searchType', SeekerSearchType.Replacement);
    component.updateDraft('searchReason', SeekerSearchReason.Missing);
    component.updateDraft('grabbed', 'false');
    component.applyFilters();
    fixture.detectChanges();

    expect(component.drawerOpen()).toBe(false);
    expect(component.selectedInstanceId()).toBe('radarr-zulu');
    expect(component.activeFilterCount()).toBe(5);
    expect(lastQuery()).toMatchObject({
      instanceId: 'radarr-zulu',
      search: 'show',
      searchStatus: [SearchCommandStatus.Failed],
      searchType: SeekerSearchType.Replacement,
      searchReason: SeekerSearchReason.Missing,
      grabbed: false,
      cycleId: undefined,
    });
  });

  it('resolves the current cycle id only for the selected instance and drops it when the instance is cleared', () => {
    const { fixture, component, lastQuery } = setup();

    component.openFilters();
    component.updateDraft('instanceId', 'radarr-zulu');
    component.updateDraft('cycleFilter', 'current');
    component.applyFilters();
    fixture.detectChanges();

    expect(lastQuery().cycleId).toBe('cycle-zulu-9999');
    expect(component.activeFilterCount()).toBe(2);

    component.openFilters();
    component.updateDraft('instanceId', '');
    expect(component.draft().cycleFilter).toBe('all');

    component.applyFilters();
    fixture.detectChanges();

    expect(lastQuery().cycleId).toBeUndefined();
    expect(lastQuery().instanceId).toBeUndefined();
    expect(component.activeFilterCount()).toBe(0);
  });

  it('maps a grabbed tri-state of any to undefined and yes to true', () => {
    const { fixture, component, lastQuery } = setup();

    component.openFilters();
    component.updateDraft('grabbed', 'true');
    component.applyFilters();
    fixture.detectChanges();
    expect(lastQuery().grabbed).toBe(true);

    component.openFilters();
    component.resetFilters();
    component.applyFilters();
    fixture.detectChanges();
    expect(lastQuery().grabbed).toBeUndefined();
  });

  it('sends every supported sort column and direction and returns to the first page', () => {
    const { fixture, component, lastQuery } = setup();

    for (const sortBy of [
      SearchEventsSortBy.Title,
      SearchEventsSortBy.Status,
      SearchEventsSortBy.Type,
      SearchEventsSortBy.Timestamp,
    ]) {
      component.onEventsPageChange(4);
      component.onSortByChange(sortBy);
      fixture.detectChanges();

      expect(lastQuery().sortBy).toBe(sortBy);
      expect(lastQuery().page).toBe(1);
    }

    component.onEventsPageChange(4);
    component.onSortOrderChange(SortDirection.Asc);
    fixture.detectChanges();
    expect(lastQuery().sortDirection).toBe(SortDirection.Asc);
    expect(lastQuery().page).toBe(1);

    component.onSortOrderChange(SortDirection.Desc);
    fixture.detectChanges();
    expect(lastQuery().sortDirection).toBe(SortDirection.Desc);
  });

  it('pages through results, resets to page one on a filter change and persists the page size', () => {
    const { fixture, component, lastQuery } = setup();

    component.onEventsPageChange(3);
    fixture.detectChanges();
    expect(lastQuery().page).toBe(3);

    component.searchQuery.set('stranger');
    component.onSearchFilterChange();
    fixture.detectChanges();
    expect(lastQuery()).toMatchObject({ page: 1, search: 'stranger' });

    component.onEventsPageChange(2);
    component.onPageSizeChange(25);
    fixture.detectChanges();
    expect(lastQuery()).toMatchObject({ page: 1, pageSize: 25 });
    expect(localStorage.getItem(PAGE_SIZE_KEY)).toBe('25');

    component.onPageSizeChange(0);
    fixture.detectChanges();
    expect(component.pageSize()).toBe(25);
  });

  it('renders a row per event with a fallback title and the truncated cycle id', () => {
    const { fixture, component } = setup();

    expect(component.eventsTotalRecords()).toBe(2);
    expect(rowTitles(fixture)).toEqual(['The Show S01E01', 'Search triggered']);
    expect(fixture.nativeElement.querySelector('.list-row__cycle').textContent.trim()).toBe('cycle-aa');
    expect(fixture.nativeElement.querySelector('.list-row__detail-text').textContent).toContain(
      'Grabbed: Release.One, Release.Two',
    );
  });

  it('derives cycle progress, health warnings and badge severities from the instance stats', () => {
    const { component } = setup();

    expect(component.cycleProgress(SONARR_BETA)).toBe(38);
    expect(component.cycleProgress(RADARR_ALPHA)).toBe(0);
    expect(component.cycleProgress(instance({ cycleItemsSearched: 12, cycleItemsTotal: 8 }))).toBe(100);

    expect(component.instanceHealthWarning(RADARR_ALPHA)).toBe('Never searched');
    expect(component.instanceHealthWarning(SONARR_BETA)).toBeNull();

    expect(component.instanceTypeSeverity('Radarr')).toBe('warning');
    expect(component.instanceTypeSeverity('Sonarr')).toBe('info');
    expect(component.instanceTypeSeverity('Lidarr')).toBe('default');

    expect(component.searchTypeSeverity(SeekerSearchType.Replacement)).toBe('warning');
    expect(component.searchTypeSeverity(SeekerSearchType.Proactive)).toBe('info');

    expect(component.searchStatusSeverity('Completed')).toBe('success');
    expect(component.searchStatusSeverity('Failed')).toBe('error');
    expect(component.searchStatusSeverity('TimedOut')).toBe('warning');
    expect(component.searchStatusSeverity('Started')).toBe('info');
    expect(component.searchStatusSeverity('Pending')).toBe('default');
  });

  it('labels every known search reason and passes unknown ones through', () => {
    const { component } = setup();

    expect(component.formatSearchReason(SeekerSearchReason.Missing)).toBe('Missing');
    expect(component.formatSearchReason(SeekerSearchReason.QualityCutoffNotMet)).toBe('Cutoff Unmet');
    expect(component.formatSearchReason(SeekerSearchReason.CustomFormatScoreBelowCutoff)).toBe('CF Below Cutoff');
    expect(component.formatSearchReason(SeekerSearchReason.Replacement)).toBe('Replacement');
    expect(component.formatSearchReason('Whatever')).toBe('Whatever');

    expect(component.searchReasonSeverity(SeekerSearchReason.Missing)).toBe('error');
    expect(component.searchReasonSeverity(SeekerSearchReason.QualityCutoffNotMet)).toBe('warning');
    expect(component.searchReasonSeverity(SeekerSearchReason.CustomFormatScoreBelowCutoff)).toBe('warning');
    expect(component.searchReasonSeverity(SeekerSearchReason.Replacement)).toBe('info');
    expect(component.searchReasonSeverity('Whatever')).toBe('default');
  });

  it('formats the cycle duration down to the largest non-zero unit', () => {
    const { component } = setup();

    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-07-31T12:00:00Z'));

    expect(component.formatCycleDuration('2026-07-29T09:00:00Z')).toBe('2d 3h');
    expect(component.formatCycleDuration('2026-07-31T07:00:00Z')).toBe('5h');
    expect(component.formatCycleDuration('2026-07-31T11:30:00Z')).toBe('30m');
  });
});
