import { Component, ChangeDetectionStrategy, inject, signal, computed, effect, untracked, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { NgIcon } from '@ng-icons/core';
import {
  CardComponent, BadgeComponent, ButtonComponent, SelectComponent,
  InputComponent, PaginatorComponent, EmptyStateComponent, TooltipComponent,
  DrawerComponent,
} from '@ui';
import type { SelectOption } from '@ui';
import type { BadgeSeverity } from '@ui/badge/badge.component';
import { AnimatedCounterComponent } from '@ui/animated-counter/animated-counter.component';
import { SearchStatsApi, SearchEventsSortBy, SortDirection } from '@core/api/search-stats.api';
import type { SearchStatsSummary, SearchEvent, InstanceSearchStat } from '@core/models/search-stats.models';
import { SeekerSearchType, SeekerSearchReason, SearchCommandStatus } from '@core/models/search-stats.models';
import { AppHubService } from '@core/realtime/app-hub.service';
import { ToastService } from '@core/services/toast.service';
import { PaginationService } from '@core/services/pagination.service';
import { StickyAwareDirective } from '@core/directives/sticky-aware.directive';

type CycleFilter = 'current' | 'all';
type TriState = 'any' | 'true' | 'false';

const DEFAULT_SORT_BY = SearchEventsSortBy.Timestamp;
const DEFAULT_SORT_DIRECTION = SortDirection.Desc;

interface AdvancedFilters {
  instanceId: string;
  cycleFilter: CycleFilter;
  statuses: SearchCommandStatus[];
  searchType: SeekerSearchType | '';
  searchReason: SeekerSearchReason | '';
  grabbed: TriState;
}

const EMPTY_FILTERS: AdvancedFilters = {
  instanceId: '',
  cycleFilter: 'current',
  statuses: [],
  searchType: '',
  searchReason: '',
  grabbed: 'any',
};

const STATUS_OPTIONS: ReadonlyArray<{ value: SearchCommandStatus; label: string }> = [
  { value: SearchCommandStatus.Started, label: 'Started' },
  { value: SearchCommandStatus.Completed, label: 'Completed' },
  { value: SearchCommandStatus.Failed, label: 'Failed' },
  { value: SearchCommandStatus.TimedOut, label: 'Timed Out' },
];

@Component({
  selector: 'app-searches-tab',
  standalone: true,
  imports: [
    DatePipe,
    NgIcon,
    CardComponent,
    BadgeComponent,
    ButtonComponent,
    SelectComponent,
    InputComponent,
    PaginatorComponent,
    EmptyStateComponent,
    AnimatedCounterComponent,
    TooltipComponent,
    DrawerComponent,
    StickyAwareDirective,
  ],
  templateUrl: './searches-tab.component.html',
  styleUrl: './searches-tab.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SearchesTabComponent implements OnInit {
  private static readonly PAGE_SIZE_KEY = 'cleanuparr-page-size-seeker-searches';

  private readonly api = inject(SearchStatsApi);
  private readonly hub = inject(AppHubService);
  private readonly toast = inject(ToastService);
  private readonly pagination = inject(PaginationService);
  private initialLoad = true;
  private latestLoadToken = 0;

  readonly summary = signal<SearchStatsSummary | null>(null);
  readonly loading = signal(false);

  readonly sortedInstanceStats = computed(() =>
    [...(this.summary()?.perInstanceStats ?? [])].sort((a, b) => {
      const typeCompare = a.instanceType.localeCompare(b.instanceType);
      return typeCompare !== 0 ? typeCompare : a.instanceName.localeCompare(b.instanceName);
    })
  );

  readonly selectedInstanceId = signal<string>('');
  readonly instanceOptions = signal<SelectOption[]>([]);

  readonly searchQuery = signal('');

  readonly sortBy = signal<SearchEventsSortBy>(DEFAULT_SORT_BY);
  readonly sortDirection = signal<SortDirection>(DEFAULT_SORT_DIRECTION);

  // Applied filters drive the query; draft lives inside the open drawer.
  readonly applied = signal<AdvancedFilters>({ ...EMPTY_FILTERS });
  readonly draft = signal<AdvancedFilters>({ ...EMPTY_FILTERS });
  readonly drawerOpen = signal(false);

  readonly events = signal<SearchEvent[]>([]);
  readonly eventsTotalRecords = signal(0);
  readonly eventsPage = signal(1);
  readonly pageSize = signal(this.pagination.getPageSize(SearchesTabComponent.PAGE_SIZE_KEY, 50));

  readonly sortOptions: SelectOption[] = [
    { label: 'Timestamp', value: SearchEventsSortBy.Timestamp },
    { label: 'Title', value: SearchEventsSortBy.Title },
    { label: 'Status', value: SearchEventsSortBy.Status },
    { label: 'Type', value: SearchEventsSortBy.Type },
  ];

  readonly sortOrderOptions: SelectOption[] = [
    { label: 'Descending', value: SortDirection.Desc },
    { label: 'Ascending', value: SortDirection.Asc },
  ];

  readonly cycleFilterOptions: SelectOption[] = [
    { label: 'Current Cycle', value: 'current' },
    { label: 'All Time', value: 'all' },
  ];

  readonly searchTypeOptions: SelectOption[] = [
    { label: 'Any', value: '' },
    { label: 'Proactive', value: SeekerSearchType.Proactive },
    { label: 'Replacement', value: SeekerSearchType.Replacement },
  ];

  readonly searchReasonOptions: SelectOption[] = [
    { label: 'Any', value: '' },
    { label: 'Missing', value: SeekerSearchReason.Missing },
    { label: 'Cutoff Unmet', value: SeekerSearchReason.QualityCutoffNotMet },
    { label: 'CF Below Cutoff', value: SeekerSearchReason.CustomFormatScoreBelowCutoff },
    { label: 'Replacement', value: SeekerSearchReason.Replacement },
  ];

  readonly triStateOptions: SelectOption[] = [
    { label: 'Any', value: 'any' },
    { label: 'Yes', value: 'true' },
    { label: 'No', value: 'false' },
  ];

  readonly statusOptions = STATUS_OPTIONS;

  readonly activeFilterCount = computed(() => {
    const a = this.applied();
    let n = 0;
    if (a.instanceId) n++;
    if (a.cycleFilter !== EMPTY_FILTERS.cycleFilter) n++;
    if (a.statuses.length) n++;
    if (a.searchType) n++;
    if (a.searchReason) n++;
    if (a.grabbed !== 'any') n++;
    return n;
  });

  constructor() {
    effect(() => {
      this.hub.searchStatsVersion();
      if (this.initialLoad) {
        this.initialLoad = false;
        return;
      }
      untracked(() => {
        this.loadSummary();
        this.loadEvents();
      });
    });
  }

  ngOnInit(): void {
    this.loadSummary();
    this.loadEvents();
  }

  onSearchFilterChange(): void {
    this.eventsPage.set(1);
    this.loadEvents();
  }

  onEventsPageChange(page: number): void {
    this.eventsPage.set(page);
    this.loadEvents();
  }

  onSortByChange(value: SearchEventsSortBy): void {
    this.sortBy.set(value);
    this.eventsPage.set(1);
    this.loadEvents();
  }

  onSortOrderChange(value: SortDirection): void {
    this.sortDirection.set(value);
    this.eventsPage.set(1);
    this.loadEvents();
  }

  readonly onPageSizeChange = this.pagination.createPageSizeHandler(
    SearchesTabComponent.PAGE_SIZE_KEY,
    this.pageSize,
    this.eventsPage,
    () => this.loadEvents(),
  );

  openFilters(): void {
    this.draft.set({ ...this.applied(), instanceId: this.selectedInstanceId() });
    this.drawerOpen.set(true);
  }

  resetFilters(): void {
    this.draft.set({ ...EMPTY_FILTERS });
  }

  applyFilters(): void {
    const draft = { ...this.draft() };
    // 'Current Cycle' only makes sense against a specific instance; without one
    // it would silently turn into an all-time query.
    if (draft.cycleFilter === 'current' && !draft.instanceId) {
      draft.cycleFilter = 'all';
    }
    this.applied.set(draft);
    this.draft.set(draft);
    this.selectedInstanceId.set(draft.instanceId);
    this.drawerOpen.set(false);
    this.eventsPage.set(1);
    this.loadEvents();
  }

  toggleStatus(value: SearchCommandStatus): void {
    this.draft.update(d => {
      const has = d.statuses.includes(value);
      return { ...d, statuses: has ? d.statuses.filter(s => s !== value) : [...d.statuses, value] };
    });
  }

  isStatusDrafted(value: SearchCommandStatus): boolean {
    return this.draft().statuses.includes(value);
  }

  updateDraft<K extends keyof AdvancedFilters>(key: K, value: AdvancedFilters[K]): void {
    this.draft.update(d => ({ ...d, [key]: value }));
  }

  refresh(): void {
    this.loadSummary();
    this.loadEvents();
  }

  searchTypeSeverity(type: SeekerSearchType): 'info' | 'warning' {
    return type === SeekerSearchType.Replacement ? 'warning' : 'info';
  }

  instanceTypeSeverity(type: string): BadgeSeverity {
    if (type === 'Radarr') return 'warning';
    if (type === 'Sonarr') return 'info';
    return 'default';
  }

  searchStatusSeverity(status: string): BadgeSeverity {
    switch (status) {
      case 'Completed': return 'success';
      case 'Failed': return 'error';
      case 'TimedOut': return 'warning';
      case 'Started': return 'info';
      default: return 'default';
    }
  }

  formatGrabbedItems(items: string[]): string {
    return items.join(', ');
  }

  formatSearchReason(reason: string): string {
    switch (reason) {
      case SeekerSearchReason.Missing: return 'Missing';
      case SeekerSearchReason.QualityCutoffNotMet: return 'Cutoff Unmet';
      case SeekerSearchReason.CustomFormatScoreBelowCutoff: return 'CF Below Cutoff';
      case SeekerSearchReason.Replacement: return 'Replacement';
      default: return reason;
    }
  }

  searchReasonSeverity(reason: string): BadgeSeverity {
    switch (reason) {
      case SeekerSearchReason.Missing: return 'error';
      case SeekerSearchReason.QualityCutoffNotMet: return 'warning';
      case SeekerSearchReason.CustomFormatScoreBelowCutoff: return 'warning';
      case SeekerSearchReason.Replacement: return 'info';
      default: return 'default';
    }
  }

  cycleProgress(inst: InstanceSearchStat): number {
    if (!inst.cycleItemsTotal) return 0;
    return Math.min(100, Math.round((inst.cycleItemsSearched / inst.cycleItemsTotal) * 100));
  }

  instanceHealthWarning(stat: InstanceSearchStat): string | null {
    if (!stat.lastSearchedAt && stat.totalSearchCount === 0) {
      return 'Never searched';
    }
    return null;
  }

  formatCycleDuration(cycleStartedAt: string): string {
    const start = new Date(cycleStartedAt);
    const now = new Date();
    const diffMs = now.getTime() - start.getTime();
    const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));
    const diffHours = Math.floor((diffMs % (1000 * 60 * 60 * 24)) / (1000 * 60 * 60));

    if (diffDays > 0) {
      return `${diffDays}d ${diffHours}h`;
    }
    if (diffHours > 0) {
      return `${diffHours}h`;
    }
    const diffMinutes = Math.floor((diffMs % (1000 * 60 * 60)) / (1000 * 60));
    return `${diffMinutes}m`;
  }

  private loadSummary(): void {
    this.api.getSummary().subscribe({
      next: (summary) => {
        this.summary.set(summary);
        this.instanceOptions.set([
          { label: 'All Instances', value: '' },
          ...summary.perInstanceStats.map(s => ({
            label: s.instanceName,
            value: s.instanceId,
          })),
        ]);
      },
      error: () => this.toast.error('Failed to load search stats'),
    });
  }

  private loadEvents(): void {
    this.loading.set(true);
    const loadToken = ++this.latestLoadToken;
    const instanceId = this.selectedInstanceId() || undefined;
    const search = this.searchQuery() || undefined;
    const a = this.applied();

    let cycleId: string | undefined;
    if (a.cycleFilter === 'current' && instanceId) {
      const instance = this.summary()?.perInstanceStats.find(s => s.instanceId === instanceId);
      cycleId = instance?.currentCycleId ?? undefined;
    }

    const triToBool = (v: TriState): boolean | undefined => v === 'any' ? undefined : v === 'true';

    this.api.getEvents({
      page: this.eventsPage(),
      pageSize: this.pageSize(),
      instanceId,
      cycleId,
      search,
      sortBy: this.sortBy(),
      sortDirection: this.sortDirection(),
      searchStatus: a.statuses.length ? a.statuses : undefined,
      searchType: a.searchType || undefined,
      searchReason: a.searchReason || undefined,
      grabbed: triToBool(a.grabbed),
    }).subscribe({
      next: (result) => {
        if (loadToken !== this.latestLoadToken) return;
        this.events.set(result.items);
        this.eventsTotalRecords.set(result.totalCount);
        this.loading.set(false);
      },
      error: () => {
        if (loadToken !== this.latestLoadToken) return;
        this.loading.set(false);
        this.toast.error('Failed to load search events');
      },
    });
  }
}
