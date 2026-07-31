import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import {
  CfScoreApi, CutoffFilter, MonitoredFilter, CfScoresSortBy, SortDirection,
} from '@core/api/cf-score.api';
import type {
  CfScoreEntriesResponse, CfScoreEntry, CfScoreHistoryResponse, CfScoreInstance,
  CfScoreStats, CfScoresQuery,
} from '@core/api/cf-score.api';
import { AppHubService } from '@core/realtime/app-hub.service';
import { QualityTabComponent } from './quality-tab.component';

const PAGE_SIZE_KEY = 'cleanuparr-page-size-seeker-quality';

const INSTANCES: CfScoreInstance[] = [
  { id: 'sonarr-1', name: 'Sonarr Main', itemType: 'Sonarr', qualityProfiles: ['HD-1080p', 'Standard'] },
  { id: 'radarr-1', name: 'Radarr Main', itemType: 'Radarr', qualityProfiles: ['Ultra-HD', 'Standard'] },
  { id: 'readarr-1', name: 'Readarr Main', itemType: 'Readarr' },
];

const STATS: CfScoreStats = {
  totalTracked: 100,
  belowCutoff: 30,
  atOrAboveCutoff: 70,
  monitored: 90,
  unmonitored: 10,
  recentUpgrades: 4,
  perInstanceStats: [
    {
      instanceId: 'sonarr-1',
      instanceName: 'Sonarr Main',
      instanceType: 'Sonarr',
      totalTracked: 60,
      belowCutoff: 20,
      atOrAboveCutoff: 40,
      monitored: 55,
      unmonitored: 5,
      recentUpgrades: 3,
    },
  ],
};

function entry(partial: Partial<CfScoreEntry> = {}): CfScoreEntry {
  return {
    id: 'entry-1',
    arrInstanceId: 'sonarr-1',
    externalItemId: 42,
    episodeId: 7,
    itemType: 'Sonarr',
    title: 'The Show',
    fileId: 1,
    currentScore: 100,
    cutoffScore: 200,
    qualityProfileName: 'HD-1080p',
    isBelowCutoff: true,
    isMonitored: true,
    lastSyncedAt: '2026-07-30T10:00:00Z',
    lastUpgradedAt: null,
    ...partial,
  };
}

const SCORES: CfScoreEntriesResponse = {
  items: [entry(), entry({ id: 'entry-2', title: 'The Movie', itemType: 'Radarr', isBelowCutoff: false })],
  page: 1,
  pageSize: 50,
  totalCount: 2,
  totalPages: 1,
};

const HISTORY: CfScoreHistoryResponse = {
  entries: [
    { score: 50, cutoffScore: 200, recordedAt: '2026-07-01T10:00:00Z' },
    { score: 100, cutoffScore: 200, recordedAt: '2026-07-20T10:00:00Z' },
  ],
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
  fixture: ComponentFixture<QualityTabComponent>;
  component: QualityTabComponent;
  queries: CfScoresQuery[];
  lastQuery: () => CfScoresQuery;
  historyCalls: [string, number, number][];
}

describe('QualityTabComponent', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    localStorage.removeItem(PAGE_SIZE_KEY);
  });

  function setup(options: { historyFails?: boolean } = {}): Harness {
    vi.stubGlobal('IntersectionObserver', IntersectionObserverStub);
    const queries: CfScoresQuery[] = [];
    const historyCalls: [string, number, number][] = [];

    TestBed.configureTestingModule({
      providers: [
        {
          provide: CfScoreApi,
          useValue: {
            getStats: () => of(STATS),
            getInstances: () => of({ instances: INSTANCES }),
            getScores: (query: CfScoresQuery) => {
              queries.push(query);
              return of(SCORES);
            },
            getItemHistory: (instanceId: string, itemId: number, episodeId: number) => {
              historyCalls.push([instanceId, itemId, episodeId]);
              if (options.historyFails) {
                return throwError(() => new Error('boom'));
              }
              return of(HISTORY);
            },
          },
        },
        {
          provide: AppHubService,
          useValue: { cfScoresVersion: signal(0) },
        },
      ],
    });

    const fixture = TestBed.createComponent(QualityTabComponent);
    fixture.detectChanges();

    return {
      fixture,
      component: fixture.componentInstance,
      queries,
      lastQuery: () => queries[queries.length - 1],
      historyCalls,
    };
  }

  it('builds the instance options from the instances resource and starts with the default query', () => {
    const { component, lastQuery } = setup();

    expect(component.instanceOptions()).toEqual([
      { label: 'All Instances', value: '' },
      { label: 'Sonarr Main (Sonarr)', value: 'sonarr-1' },
      { label: 'Radarr Main (Radarr)', value: 'radarr-1' },
      { label: 'Readarr Main (Readarr)', value: 'readarr-1' },
    ]);
    expect(component.activeFilterCount()).toBe(0);
    expect(lastQuery()).toEqual({
      page: 1,
      pageSize: 50,
      search: undefined,
      instanceId: undefined,
      sortBy: CfScoresSortBy.Title,
      sortDirection: SortDirection.Asc,
      qualityProfile: undefined,
      cutoffFilter: CutoffFilter.All,
      monitoredFilter: MonitoredFilter.All,
    });
  });

  it('shows the global stats until an instance is selected and null for an instance with no stats', () => {
    const { fixture, component } = setup();

    expect(component.displayStats()).toBe(STATS);

    component.selectedInstanceId.set('sonarr-1');
    fixture.detectChanges();
    expect(component.displayStats()).toEqual(STATS.perInstanceStats[0]);

    component.selectedInstanceId.set('radarr-1');
    fixture.detectChanges();
    expect(component.displayStats()).toBeNull();
  });

  it('offers the deduped union of quality profiles and narrows it to the drafted instance while the drawer is open', () => {
    const { fixture, component } = setup();

    expect(component.qualityProfileOptions()).toEqual([
      { label: 'All profiles', value: '' },
      { label: 'HD-1080p', value: 'HD-1080p' },
      { label: 'Standard', value: 'Standard' },
      { label: 'Ultra-HD', value: 'Ultra-HD' },
    ]);

    component.openFilters();
    component.updateDraft('instanceId', 'radarr-1');
    fixture.detectChanges();

    expect(component.qualityProfileOptions()).toEqual([
      { label: 'All profiles', value: '' },
      { label: 'Standard', value: 'Standard' },
      { label: 'Ultra-HD', value: 'Ultra-HD' },
    ]);
  });

  it('drops a drafted quality profile that does not belong to the drafted instance', () => {
    const { fixture, component, lastQuery } = setup();

    component.openFilters();
    component.updateDraft('qualityProfile', 'HD-1080p');
    component.updateDraft('instanceId', 'radarr-1');
    component.applyFilters();
    fixture.detectChanges();

    expect(component.applied().qualityProfile).toBe('');
    expect(lastQuery()).toMatchObject({ instanceId: 'radarr-1', qualityProfile: undefined });

    component.openFilters();
    component.updateDraft('qualityProfile', 'Ultra-HD');
    component.applyFilters();
    fixture.detectChanges();

    expect(lastQuery()).toMatchObject({ instanceId: 'radarr-1', qualityProfile: 'Ultra-HD' });
  });

  it('feeds the applied cutoff and monitored filters into the query and counts them', () => {
    const { fixture, component, lastQuery } = setup();

    component.searchQuery.set('show');
    component.openFilters();
    component.updateDraft('instanceId', 'sonarr-1');
    component.updateDraft('qualityProfile', 'HD-1080p');
    component.updateDraft('cutoffFilter', CutoffFilter.Below);
    component.updateDraft('monitoredFilter', MonitoredFilter.Unmonitored);
    component.applyFilters();
    fixture.detectChanges();

    expect(component.drawerOpen()).toBe(false);
    expect(component.activeFilterCount()).toBe(4);
    expect(lastQuery()).toMatchObject({
      page: 1,
      search: 'show',
      instanceId: 'sonarr-1',
      qualityProfile: 'HD-1080p',
      cutoffFilter: CutoffFilter.Below,
      monitoredFilter: MonitoredFilter.Unmonitored,
    });

    component.openFilters();
    component.resetFilters();
    component.applyFilters();
    fixture.detectChanges();

    expect(component.activeFilterCount()).toBe(0);
    expect(lastQuery()).toMatchObject({
      instanceId: undefined,
      qualityProfile: undefined,
      cutoffFilter: CutoffFilter.All,
      monitoredFilter: MonitoredFilter.All,
    });
  });

  it('sends every supported sort column and direction and returns to the first page', () => {
    const { fixture, component, lastQuery } = setup();

    for (const sortBy of [
      CfScoresSortBy.CurrentScore,
      CfScoresSortBy.CutoffScore,
      CfScoresSortBy.QualityProfile,
      CfScoresSortBy.LastSyncedAt,
      CfScoresSortBy.LastUpgradedAt,
      CfScoresSortBy.Title,
    ]) {
      component.onPageChange(5);
      component.onSortByChange(sortBy);
      fixture.detectChanges();

      expect(lastQuery()).toMatchObject({ sortBy, page: 1 });
    }

    component.onPageChange(5);
    component.onSortOrderChange(SortDirection.Desc);
    fixture.detectChanges();
    expect(lastQuery()).toMatchObject({ sortDirection: SortDirection.Desc, page: 1 });
  });

  it('pages through results, resets to page one on a filter change and persists the page size', () => {
    const { fixture, component, lastQuery } = setup();

    component.onPageChange(3);
    fixture.detectChanges();
    expect(lastQuery().page).toBe(3);

    component.searchQuery.set('movie');
    component.onFilterChange();
    fixture.detectChanges();
    expect(lastQuery()).toMatchObject({ page: 1, search: 'movie' });

    component.onPageChange(2);
    component.onPageSizeChange(10);
    fixture.detectChanges();
    expect(lastQuery()).toMatchObject({ page: 1, pageSize: 10 });
    expect(localStorage.getItem(PAGE_SIZE_KEY)).toBe('10');
  });

  it('loads the score history when a row expands and clears it when the same row collapses', () => {
    const { fixture, component, historyCalls } = setup();

    component.toggleExpand(SCORES.items[0]);
    fixture.detectChanges();

    expect(historyCalls).toEqual([['sonarr-1', 42, 7]]);
    expect(component.expandedId()).toBe('entry-1');
    expect(component.historyLoading()).toBe(false);
    expect(component.historyEntries()).toEqual(HISTORY.entries);

    component.toggleExpand(SCORES.items[0]);
    fixture.detectChanges();

    expect(component.expandedId()).toBeNull();
    expect(component.historyEntries()).toEqual([]);
    expect(historyCalls).toHaveLength(1);
  });

  it('stops the history spinner when the history request fails', () => {
    const { fixture, component } = setup({ historyFails: true });

    component.toggleExpand(SCORES.items[0]);
    fixture.detectChanges();

    expect(component.historyLoading()).toBe(false);
    expect(component.historyEntries()).toEqual([]);
  });

  it('renders a row per score with its status and monitored labels', () => {
    const { fixture, component } = setup();

    expect(component.totalRecords()).toBe(2);
    const titles = Array.from(fixture.nativeElement.querySelectorAll('.score-row__title')).map((row) =>
      (row as HTMLElement).textContent!.trim(),
    );
    expect(titles).toEqual(['The Show', 'The Movie']);

    expect(component.statusLabel(true)).toBe('Below Cutoff');
    expect(component.statusLabel(false)).toBe('Met');
    expect(component.statusSeverity(true)).toBe('warning');
    expect(component.statusSeverity(false)).toBe('success');
    expect(component.itemTypeSeverity('Sonarr')).toBe('info');
    expect(component.itemTypeSeverity('Radarr')).toBe('info');
    expect(component.itemTypeSeverity('Readarr')).toBe('default');
  });
});
