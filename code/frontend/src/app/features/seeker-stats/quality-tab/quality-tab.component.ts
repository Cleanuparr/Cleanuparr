import { Component, ChangeDetectionStrategy, inject, signal, computed, effect } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import type { Observable } from 'rxjs';
import { DatePipe } from '@angular/common';
import { NgIcon } from '@ng-icons/core';
import {
  CardComponent, BadgeComponent, ButtonComponent, InputComponent,
  PaginatorComponent, EmptyStateComponent, SelectComponent,
  TooltipComponent, DrawerComponent,
} from '@ui';
import type { SelectOption } from '@ui';
import { AnimatedCounterComponent } from '@ui/animated-counter/animated-counter.component';
import {
  CfScoreApi, CfScoreEntry, CfScoreStats, CfScoreHistoryEntry, CfScoreInstance,
  CfScoreEntriesResponse, CfScoresQuery,
  CutoffFilter, MonitoredFilter, CfScoresSortBy, SortDirection,
} from '@core/api/cf-score.api';
import { AppHubService } from '@core/realtime/app-hub.service';
import { ToastService } from '@core/services/toast.service';
import { PaginationService } from '@core/services/pagination.service';
import { StickyAwareDirective } from '@core/directives/sticky-aware.directive';

const DEFAULT_SORT_BY = CfScoresSortBy.Title;
const DEFAULT_SORT_DIRECTION = SortDirection.Asc;

interface AdvancedFilters {
  instanceId: string;
  qualityProfile: string;
  cutoffFilter: CutoffFilter;
  monitoredFilter: MonitoredFilter;
}

const EMPTY_FILTERS: AdvancedFilters = {
  instanceId: '',
  qualityProfile: '',
  cutoffFilter: CutoffFilter.All,
  monitoredFilter: MonitoredFilter.All,
};

@Component({
  selector: 'app-quality-tab',
  standalone: true,
  imports: [
    DatePipe,
    NgIcon,
    CardComponent,
    BadgeComponent,
    ButtonComponent,
    InputComponent,
    SelectComponent,
    PaginatorComponent,
    EmptyStateComponent,
    AnimatedCounterComponent,
    TooltipComponent,
    DrawerComponent,
    StickyAwareDirective,
  ],
  templateUrl: './quality-tab.component.html',
  styleUrl: './quality-tab.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class QualityTabComponent {
  private static readonly PAGE_SIZE_KEY = 'cleanuparr-page-size-seeker-quality';

  private readonly api = inject(CfScoreApi);
  private readonly hub = inject(AppHubService);
  private readonly toast = inject(ToastService);
  private readonly pagination = inject(PaginationService);
  private initialLoad = true;

  readonly currentPage = signal(1);
  readonly pageSize = signal(this.pagination.getPageSize(QualityTabComponent.PAGE_SIZE_KEY, 50));
  readonly searchQuery = signal('');
  readonly selectedInstanceId = signal<string>('');

  readonly sortBy = signal<CfScoresSortBy>(DEFAULT_SORT_BY);
  readonly sortDirection = signal<SortDirection>(DEFAULT_SORT_DIRECTION);

  readonly sortOptions: SelectOption[] = [
    { label: 'Title', value: CfScoresSortBy.Title },
    { label: 'Current Score', value: CfScoresSortBy.CurrentScore },
    { label: 'Cutoff', value: CfScoresSortBy.CutoffScore },
    { label: 'Quality Profile', value: CfScoresSortBy.QualityProfile },
    { label: 'Last Synced', value: CfScoresSortBy.LastSyncedAt },
    { label: 'Last Upgraded', value: CfScoresSortBy.LastUpgradedAt },
  ];

  readonly sortOrderOptions: SelectOption[] = [
    { label: 'Ascending', value: SortDirection.Asc },
    { label: 'Descending', value: SortDirection.Desc },
  ];

  readonly applied = signal<AdvancedFilters>({ ...EMPTY_FILTERS });
  readonly draft = signal<AdvancedFilters>({ ...EMPTY_FILTERS });
  readonly drawerOpen = signal(false);

  private readonly scoresParams = computed<CfScoresQuery>(() => {
    const a = this.applied();
    return {
      page: this.currentPage(),
      pageSize: this.pageSize(),
      search: this.searchQuery() || undefined,
      instanceId: this.selectedInstanceId() || undefined,
      sortBy: this.sortBy(),
      sortDirection: this.sortDirection(),
      qualityProfile: a.qualityProfile || undefined,
      cutoffFilter: a.cutoffFilter,
      monitoredFilter: a.monitoredFilter,
    };
  });

  private readonly scoresResource = rxResource({
    params: () => this.scoresParams(),
    stream: ({ params }) => this.api.getScores(params),
    defaultValue: { items: [], page: 1, pageSize: 50, totalCount: 0, totalPages: 0 } as CfScoreEntriesResponse,
  });

  private readonly statsResource = rxResource({
    stream: (): Observable<CfScoreStats | null> => this.api.getStats(),
    defaultValue: null,
  });

  private readonly instancesResource = rxResource({
    stream: () => this.api.getInstances(),
    defaultValue: { instances: [] as CfScoreInstance[] },
  });

  readonly items = computed(() => this.scoresResource.value().items);
  readonly totalRecords = computed(() => this.scoresResource.value().totalCount);
  readonly stats = computed(() => this.statsResource.value());
  readonly instances = computed(() => this.instancesResource.value().instances);
  readonly instanceOptions = computed<SelectOption[]>(() => [
    { label: 'All Instances', value: '' },
    ...this.instancesResource.value().instances.map((i) => ({ label: `${i.name} (${i.itemType})`, value: i.id })),
  ]);

  readonly displayStats = computed(() => {
    const s = this.stats();
    if (!s) return null;
    const instanceId = this.selectedInstanceId();
    if (instanceId) {
      return s.perInstanceStats.find(i => i.instanceId === instanceId) ?? null;
    }
    return s;
  });

  readonly expandedId = signal<string | null>(null);
  readonly historyEntries = signal<CfScoreHistoryEntry[]>([]);
  readonly historyLoading = signal(false);

  readonly cutoffOptions: SelectOption[] = [
    { label: 'Any', value: CutoffFilter.All },
    { label: 'Below cutoff', value: CutoffFilter.Below },
    { label: 'Met cutoff', value: CutoffFilter.Met },
  ];

  readonly monitoredOptions: SelectOption[] = [
    { label: 'Any', value: MonitoredFilter.All },
    { label: 'Monitored only', value: MonitoredFilter.Monitored },
    { label: 'Unmonitored only', value: MonitoredFilter.Unmonitored },
  ];

  readonly qualityProfileOptions = computed<SelectOption[]>(() => {
    // Narrow to the drafted instance while the drawer is open so the profile
    // list stays consistent with the instance the user is composing.
    const instanceId = this.drawerOpen() ? this.draft().instanceId : this.selectedInstanceId();
    const profiles = new Set<string>();
    for (const inst of this.instances()) {
      if (instanceId && inst.id !== instanceId) continue;
      for (const p of inst.qualityProfiles ?? []) {
        profiles.add(p);
      }
    }
    const sorted = [...profiles].sort((a, b) => a.localeCompare(b));
    return [
      { label: 'Any', value: '' },
      ...sorted.map(p => ({ label: p, value: p })),
    ];
  });

  readonly activeFilterCount = computed(() => {
    const a = this.applied();
    let n = 0;
    if (a.instanceId) n++;
    if (a.qualityProfile) n++;
    if (a.cutoffFilter !== CutoffFilter.All) n++;
    if (a.monitoredFilter !== MonitoredFilter.All) n++;
    return n;
  });

  constructor() {
    effect(() => {
      this.hub.cfScoresVersion();
      if (this.initialLoad) {
        this.initialLoad = false;
        return;
      }
      this.scoresResource.reload();
      this.statsResource.reload();
    });
    effect(() => {
      if (this.scoresResource.error()) {
        this.toast.error('Failed to load CF scores');
      }
    });
    effect(() => {
      if (this.statsResource.error()) {
        this.toast.error('Failed to load CF score stats');
      }
    });
    effect(() => {
      if (this.instancesResource.error()) {
        this.toast.error('Failed to load instances');
      }
    });
  }

  onFilterChange(): void {
    this.currentPage.set(1);
  }

  onSortByChange(value: CfScoresSortBy): void {
    this.sortBy.set(value);
    this.currentPage.set(1);
  }

  onSortOrderChange(value: SortDirection): void {
    this.sortDirection.set(value);
    this.currentPage.set(1);
  }

  onPageChange(page: number): void {
    this.currentPage.set(page);
  }

  readonly onPageSizeChange = this.pagination.createPageSizeHandler(
    QualityTabComponent.PAGE_SIZE_KEY,
    this.pageSize,
    this.currentPage,
    () => this.scoresResource.reload(),
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
    // Quality profile options narrow to the chosen instance — clear any stale
    // selection that no longer belongs to the drafted instance's profiles.
    if (draft.qualityProfile) {
      const profiles = this.collectProfilesFor(draft.instanceId);
      if (!profiles.has(draft.qualityProfile)) {
        draft.qualityProfile = '';
      }
    }
    this.applied.set(draft);
    this.selectedInstanceId.set(draft.instanceId);
    this.drawerOpen.set(false);
    this.currentPage.set(1);
  }

  private collectProfilesFor(instanceId: string): Set<string> {
    const profiles = new Set<string>();
    for (const inst of this.instances()) {
      if (instanceId && inst.id !== instanceId) continue;
      for (const p of inst.qualityProfiles ?? []) {
        profiles.add(p);
      }
    }
    return profiles;
  }

  updateDraft<K extends keyof AdvancedFilters>(key: K, value: AdvancedFilters[K]): void {
    this.draft.update(d => ({ ...d, [key]: value }));
  }

  refresh(): void {
    this.scoresResource.reload();
    this.statsResource.reload();
  }

  toggleExpand(item: CfScoreEntry): void {
    const id = item.id;
    if (this.expandedId() === id) {
      this.expandedId.set(null);
      this.historyEntries.set([]);
      return;
    }

    this.expandedId.set(id);
    this.historyLoading.set(true);
    this.historyEntries.set([]);

    this.api.getItemHistory(item.arrInstanceId, item.externalItemId, item.episodeId).subscribe({
      next: (res) => {
        this.historyEntries.set(res.entries);
        this.historyLoading.set(false);
      },
      error: () => {
        this.historyLoading.set(false);
        this.toast.error('Failed to load score history');
      },
    });
  }

  statusSeverity(isBelowCutoff: boolean): 'warning' | 'success' {
    return isBelowCutoff ? 'warning' : 'success';
  }

  statusLabel(isBelowCutoff: boolean): string {
    return isBelowCutoff ? 'Below Cutoff' : 'Met';
  }

  itemTypeSeverity(itemType: string): 'info' | 'default' {
    return itemType === 'Radarr' || itemType === 'Sonarr' ? 'info' : 'default';
  }
}
