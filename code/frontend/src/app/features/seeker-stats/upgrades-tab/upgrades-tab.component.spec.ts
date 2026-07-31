import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { CfScoreApi, CfUpgradesSortBy, SortDirection } from '@core/api/cf-score.api';
import type { CfScoreInstance, CfScoreUpgradesQuery, CfScoreUpgradesResponse } from '@core/api/cf-score.api';
import { AppHubService } from '@core/realtime/app-hub.service';
import { UpgradesTabComponent } from './upgrades-tab.component';

const PAGE_SIZE_KEY = 'cleanuparr-page-size-seeker-upgrades';

const INSTANCES: CfScoreInstance[] = [
  { id: 'sonarr-1', name: 'Sonarr Main', itemType: 'Sonarr' },
  { id: 'radarr-1', name: 'Radarr Main', itemType: 'Radarr' },
];

const UPGRADES: CfScoreUpgradesResponse = {
  items: [
    {
      arrInstanceId: 'sonarr-1',
      externalItemId: 1,
      episodeId: 3,
      itemType: 'Sonarr',
      title: 'The Show',
      previousScore: 10,
      newScore: 120,
      cutoffScore: 100,
      upgradedAt: '2026-07-30T10:00:00Z',
    },
    {
      arrInstanceId: 'radarr-1',
      externalItemId: 2,
      episodeId: 0,
      itemType: 'Radarr',
      title: 'The Movie',
      previousScore: 0,
      newScore: 50,
      cutoffScore: 100,
      upgradedAt: '2026-07-29T10:00:00Z',
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
  fixture: ComponentFixture<UpgradesTabComponent>;
  component: UpgradesTabComponent;
  queries: CfScoreUpgradesQuery[];
  lastQuery: () => CfScoreUpgradesQuery;
}

describe('UpgradesTabComponent', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    localStorage.removeItem(PAGE_SIZE_KEY);
  });

  function setup(): Harness {
    vi.stubGlobal('IntersectionObserver', IntersectionObserverStub);
    const queries: CfScoreUpgradesQuery[] = [];

    TestBed.configureTestingModule({
      providers: [
        {
          provide: CfScoreApi,
          useValue: {
            getInstances: () => of({ instances: INSTANCES }),
            getRecentUpgrades: (query: CfScoreUpgradesQuery) => {
              queries.push(query);
              return of(UPGRADES);
            },
          },
        },
        {
          provide: AppHubService,
          useValue: { cfScoresVersion: signal(0) },
        },
      ],
    });

    const fixture = TestBed.createComponent(UpgradesTabComponent);
    fixture.detectChanges();

    return {
      fixture,
      component: fixture.componentInstance,
      queries,
      lastQuery: () => queries[queries.length - 1],
    };
  }

  it('starts on the last thirty days with no active filters', () => {
    const { component, lastQuery } = setup();

    expect(component.activeFilterCount()).toBe(0);
    expect(lastQuery()).toEqual({
      page: 1,
      pageSize: 50,
      instanceId: undefined,
      days: 30,
      search: undefined,
      sortBy: CfUpgradesSortBy.UpgradedAt,
      sortDirection: SortDirection.Desc,
    });
  });

  it('builds the instance options from the instances resource', () => {
    const { component } = setup();

    expect(component.instanceOptions()).toEqual([
      { label: 'All Instances', value: '' },
      { label: 'Sonarr Main (Sonarr)', value: 'sonarr-1' },
      { label: 'Radarr Main (Radarr)', value: 'radarr-1' },
    ]);
  });

  it('applies the instance and time range from the drawer and counts them as active filters', () => {
    const { fixture, component, lastQuery } = setup();

    component.openFilters();
    component.updateDraft('instanceId', 'radarr-1');
    component.updateDraft('timeRange', '7');
    component.applyFilters();
    fixture.detectChanges();

    expect(component.drawerOpen()).toBe(false);
    expect(component.selectedInstanceId()).toBe('radarr-1');
    expect(component.activeFilterCount()).toBe(2);
    expect(lastQuery()).toMatchObject({ instanceId: 'radarr-1', days: 7, page: 1 });

    component.openFilters();
    component.resetFilters();
    component.applyFilters();
    fixture.detectChanges();

    expect(component.activeFilterCount()).toBe(0);
    expect(lastQuery()).toMatchObject({ instanceId: undefined, days: 30 });
  });

  it('sends zero days for all time and omits the day count when the range is not a number', () => {
    const { fixture, component, lastQuery } = setup();

    component.openFilters();
    component.updateDraft('timeRange', '0');
    component.applyFilters();
    fixture.detectChanges();
    expect(lastQuery().days).toBe(0);
    expect(component.activeFilterCount()).toBe(1);

    component.openFilters();
    component.updateDraft('timeRange', '');
    component.applyFilters();
    fixture.detectChanges();
    expect(lastQuery().days).toBeUndefined();
  });

  it('sends every supported sort column and direction and returns to the first page', () => {
    const { fixture, component, lastQuery } = setup();

    for (const sortBy of [
      CfUpgradesSortBy.Title,
      CfUpgradesSortBy.NewScore,
      CfUpgradesSortBy.PreviousScore,
      CfUpgradesSortBy.ScoreDelta,
      CfUpgradesSortBy.CutoffScore,
      CfUpgradesSortBy.UpgradedAt,
    ]) {
      component.onPageChange(6);
      component.onSortByChange(sortBy);
      fixture.detectChanges();

      expect(lastQuery()).toMatchObject({ sortBy, page: 1 });
    }

    component.onPageChange(6);
    component.onSortOrderChange(SortDirection.Asc);
    fixture.detectChanges();
    expect(lastQuery()).toMatchObject({ sortDirection: SortDirection.Asc, page: 1 });
  });

  it('pages through results, resets to page one on a search change and persists the page size', () => {
    const { fixture, component, lastQuery } = setup();

    component.onPageChange(4);
    fixture.detectChanges();
    expect(lastQuery().page).toBe(4);

    component.searchQuery.set('movie');
    component.onSearchFilterChange();
    fixture.detectChanges();
    expect(lastQuery()).toMatchObject({ page: 1, search: 'movie' });

    component.onPageChange(2);
    component.onPageSizeChange(20);
    fixture.detectChanges();
    expect(lastQuery()).toMatchObject({ page: 1, pageSize: 20 });
    expect(localStorage.getItem(PAGE_SIZE_KEY)).toBe('20');

    component.onPageSizeChange(-5);
    fixture.detectChanges();
    expect(component.pageSize()).toBe(20);
  });

  it('renders a row per upgrade and reports the total from the response', () => {
    const { fixture, component } = setup();

    expect(component.totalRecords()).toBe(2);
    const titles = Array.from(fixture.nativeElement.querySelectorAll('.upgrade-row__title')).map((row) =>
      (row as HTMLElement).textContent!.trim(),
    );
    expect(titles).toEqual(['The Show', 'The Movie']);

    expect(component.itemTypeSeverity('Sonarr')).toBe('info');
    expect(component.itemTypeSeverity('Radarr')).toBe('info');
    expect(component.itemTypeSeverity('Whisparr')).toBe('default');
  });
});
