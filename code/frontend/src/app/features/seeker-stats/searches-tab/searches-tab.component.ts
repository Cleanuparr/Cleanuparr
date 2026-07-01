import { Component, ChangeDetectionStrategy, inject, signal, computed, effect } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import type { Observable } from 'rxjs';
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
import type { SearchEventsQuery } from '@core/api/search-stats.api';
import type { SearchStatsSummary, SearchEvent, InstanceSearchStat } from '@core/models/search-stats.models';
import type { PaginatedResult } from '@core/models/pagination.model';
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
  cycleFilter: 'all',
  statuses: [],
  searchType: '',
  searchReason: '',
  grabbed: 'any',
};

const STATUS_OPTIONS: readonly { value: SearchCommandStatus; label: string }[] = [
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
export class SearchesTabComponent {
  private static readonly PAGE_SIZE_KEY = 'cleanuparr-page-size-seeker-searches';

  private readonly api = inject(SearchStatsApi);
  private readonly hub = inject(AppHubService);
  private readonly toast = inject(ToastService);
  private readonly pagination = inject(PaginationService);
  private initialLoad = true;

  private readonly summaryResource = rxResource({
    stream: (): Observable<SearchStatsSummary | null> => this.api.getSummary(),
    defaultValue: null,
  });

  readonly summary = computed(() => this.summaryResource.value());

  readonly sortedInstanceStats = computed(() =>
    [...(this.summary()?.perInstanceStats ?? [])].sort((a, b) => {
      const typeCompare = a.instanceType.localeCompare(b.instanceType);
      return typeCompare !== 0 ? typeCompare : a.instanceName.localeCompare(b.instanceName);
    })
  );

  readonly selectedInstanceId = signal<string>('');
  readonly instanceOptions = computed<SelectOption[]>(() => {
    return [
      { label: 'All Instances', value: '' },
      ...(this.summaryResource.value()?.perInstanceStats ?? []).map((st) => ({ label: st.instanceName, value: st.instanceId })),
    ];
  });

  readonly searchQuery = signal('');

  readonly sortBy = signal<SearchEventsSortBy>(DEFAULT_SORT_BY);
  readonly sortDirection = signal<SortDirection>(DEFAULT_SORT_DIRECTION);

  // Applied filters drive the query; draft lives inside the open drawer.
  readonly applied = signal<AdvancedFilters>({ ...EMPTY_FILTERS });
  readonly draft = signal<AdvancedFilters>({ ...EMPTY_FILTERS });
  readonly drawerOpen = signal(false);

  readonly eventsPage = signal(1);
  readonly pageSize = signal(this.pagination.getPageSize(SearchesTabComponent.PAGE_SIZE_KEY, 50));

  private readonly eventsParams = computed<SearchEventsQuery>(() => {
    const instanceId = this.selectedInstanceId() || undefined;
    const search = this.searchQuery() || undefined;
    const a = this.applied();

    let cycleId: string | undefined;
    if (a.cycleFilter === 'current' && instanceId) {
      const instance = this.summaryResource.value()?.perInstanceStats.find((s) => s.instanceId === instanceId);
      cycleId = instance?.currentCycleId ?? undefined;
    }

    const triToBool = (v: TriState): boolean | undefined => (v === 'any' ? undefined : v === 'true');

    return {
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
    };
  });

  private readonly eventsResource = rxResource({
    params: () => this.eventsParams(),
    stream: ({ params }) => this.api.getEvents(params),
    defaultValue: { items: [], page: 1, pageSize: 50, totalCount: 0, totalPages: 0 } as PaginatedResult<SearchEvent>,
  });

  readonly events = computed(() => this.eventsResource.value().items);
  readonly eventsTotalRecords = computed(() => this.eventsResource.value().totalCount);

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
      this.summaryResource.reload();
      this.eventsResource.reload();
    });
    effect(() => {
      if (this.summaryResource.error()) {
        this.toast.error('Failed to load search stats');
      }
    });
    effect(() => {
      if (this.eventsResource.error()) {
        this.toast.error('Failed to load search events');
      }
    });
  }

  onSearchFilterChange(): void {
    this.eventsPage.set(1);
  }

  onEventsPageChange(page: number): void {
    this.eventsPage.set(page);
  }

  onSortByChange(value: SearchEventsSortBy): void {
    this.sortBy.set(value);
    this.eventsPage.set(1);
  }

  onSortOrderChange(value: SortDirection): void {
    this.sortDirection.set(value);
    this.eventsPage.set(1);
  }

  readonly onPageSizeChange = this.pagination.createPageSizeHandler(
    SearchesTabComponent.PAGE_SIZE_KEY,
    this.pageSize,
    this.eventsPage,
    () => this.eventsResource.reload(),
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
    this.applied.set(draft);
    this.selectedInstanceId.set(draft.instanceId);
    this.drawerOpen.set(false);
    this.eventsPage.set(1);
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
    this.draft.update(d => {
      const next = { ...d, [key]: value };
      // 'Current Cycle' only makes sense against a specific instance — clearing
      // the instance must fall the cycle filter back to 'All Time'.
      if (key === 'instanceId' && !value && next.cycleFilter === 'current') {
        next.cycleFilter = 'all';
      }
      return next;
    });
  }

  refresh(): void {
    this.summaryResource.reload();
    this.eventsResource.reload();
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
}
