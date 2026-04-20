import { Component, ChangeDetectionStrategy, inject, signal, computed, effect, untracked, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { NgIcon } from '@ng-icons/core';
import {
  CardComponent, BadgeComponent, ButtonComponent, InputComponent,
  PaginatorComponent, EmptyStateComponent, SelectComponent,
  TooltipComponent, DrawerComponent, TableComponent, ThComponent, TdComponent,
} from '@ui';
import type { SelectOption, SortState } from '@ui';
import { AnimatedCounterComponent } from '@ui/animated-counter/animated-counter.component';
import {
  CfScoreApi, CfScoreEntry, CfScoreStats, CfScoreHistoryEntry, CfScoreInstance,
  CutoffFilter, MonitoredFilter, CfScoresSortBy, SortDirection,
} from '@core/api/cf-score.api';
import { AppHubService } from '@core/realtime/app-hub.service';
import { ToastService } from '@core/services/toast.service';
import { PaginationService } from '@core/services/pagination.service';

const DEFAULT_SORT_BY = CfScoresSortBy.Title;
const DEFAULT_SORT_DIRECTION = SortDirection.Asc;

interface AdvancedFilters {
  qualityProfile: string;
  itemType: string;
  cutoffFilter: CutoffFilter;
  monitoredFilter: MonitoredFilter;
}

const EMPTY_FILTERS: AdvancedFilters = {
  qualityProfile: '',
  itemType: '',
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
    TableComponent,
    ThComponent,
    TdComponent,
  ],
  templateUrl: './quality-tab.component.html',
  styleUrl: './quality-tab.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class QualityTabComponent implements OnInit {
  private static readonly PAGE_SIZE_KEY = 'cleanuparr-page-size-seeker-quality';

  private readonly api = inject(CfScoreApi);
  private readonly hub = inject(AppHubService);
  private readonly toast = inject(ToastService);
  private readonly pagination = inject(PaginationService);
  private initialLoad = true;

  readonly items = signal<CfScoreEntry[]>([]);
  readonly stats = signal<CfScoreStats | null>(null);
  readonly totalRecords = signal(0);
  readonly loading = signal(false);

  readonly currentPage = signal(1);
  readonly pageSize = signal(this.pagination.getPageSize(QualityTabComponent.PAGE_SIZE_KEY, 50));
  readonly searchQuery = signal('');
  readonly selectedInstanceId = signal<string>('');
  readonly instances = signal<CfScoreInstance[]>([]);
  readonly instanceOptions = signal<SelectOption[]>([]);

  readonly sortBy = signal<CfScoresSortBy>(DEFAULT_SORT_BY);
  readonly sortDirection = signal<SortDirection>(DEFAULT_SORT_DIRECTION);
  readonly Sort = CfScoresSortBy;

  readonly applied = signal<AdvancedFilters>({ ...EMPTY_FILTERS });
  readonly draft = signal<AdvancedFilters>({ ...EMPTY_FILTERS });
  readonly drawerOpen = signal(false);

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

  readonly itemTypeOptions: SelectOption[] = [
    { label: 'Any', value: '' },
    { label: 'Radarr', value: 'Radarr' },
    { label: 'Sonarr', value: 'Sonarr' },
    { label: 'Lidarr', value: 'Lidarr' },
    { label: 'Readarr', value: 'Readarr' },
    { label: 'Whisparr', value: 'Whisparr' },
  ];

  readonly qualityProfileOptions = computed<SelectOption[]>(() => {
    const instanceId = this.selectedInstanceId();
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
    if (a.qualityProfile) n++;
    if (a.itemType) n++;
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
      untracked(() => {
        this.loadScores();
        this.loadStats();
      });
    });
  }

  ngOnInit(): void {
    this.loadInstances();
    this.loadScores();
    this.loadStats();
  }

  loadScores(): void {
    this.loading.set(true);
    const a = this.applied();
    this.api.getScores({
      page: this.currentPage(),
      pageSize: this.pageSize(),
      search: this.searchQuery() || undefined,
      instanceId: this.selectedInstanceId() || undefined,
      sortBy: this.sortBy(),
      sortDirection: this.sortDirection(),
      qualityProfile: a.qualityProfile || undefined,
      itemType: a.itemType || undefined,
      cutoffFilter: a.cutoffFilter,
      monitoredFilter: a.monitoredFilter,
    }).subscribe({
      next: (result) => {
        this.items.set(result.items);
        this.totalRecords.set(result.totalCount);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.error('Failed to load CF scores');
      },
    });
  }

  private loadInstances(): void {
    this.api.getInstances().subscribe({
      next: (result) => {
        this.instances.set(result.instances);
        this.instanceOptions.set([
          { label: 'All Instances', value: '' },
          ...result.instances.map(i => ({
            label: `${i.name} (${i.itemType})`,
            value: i.id,
          })),
        ]);
      },
      error: () => this.toast.error('Failed to load instances'),
    });
  }

  onInstanceFilterChange(value: string): void {
    this.selectedInstanceId.set(value);
    // Quality profile options narrow to the chosen instance — clear any stale selection.
    if (this.applied().qualityProfile) {
      const stillValid = this.qualityProfileOptions().some(o => o.value === this.applied().qualityProfile);
      if (!stillValid) {
        this.applied.update(a => ({ ...a, qualityProfile: '' }));
      }
    }
    this.currentPage.set(1);
    this.loadScores();
  }

  private loadStats(): void {
    this.api.getStats().subscribe({
      next: (stats) => this.stats.set(stats),
      error: () => this.toast.error('Failed to load CF score stats'),
    });
  }

  onFilterChange(): void {
    this.currentPage.set(1);
    this.loadScores();
  }

  onSortChange(state: SortState): void {
    this.sortBy.set((state.sortKey as CfScoresSortBy | null) ?? DEFAULT_SORT_BY);
    this.sortDirection.set(state.sortKey ? state.sortDirection : DEFAULT_SORT_DIRECTION);
    this.currentPage.set(1);
    this.loadScores();
  }

  onPageChange(page: number): void {
    this.currentPage.set(page);
    this.loadScores();
  }

  readonly onPageSizeChange = this.pagination.createPageSizeHandler(
    QualityTabComponent.PAGE_SIZE_KEY,
    this.pageSize,
    this.currentPage,
    () => this.loadScores(),
  );

  openFilters(): void {
    this.draft.set({ ...this.applied() });
    this.drawerOpen.set(true);
  }

  resetFilters(): void {
    this.draft.set({ ...EMPTY_FILTERS });
  }

  applyFilters(): void {
    this.applied.set({ ...this.draft() });
    this.drawerOpen.set(false);
    this.currentPage.set(1);
    this.loadScores();
  }

  updateDraft<K extends keyof AdvancedFilters>(key: K, value: AdvancedFilters[K]): void {
    this.draft.update(d => ({ ...d, [key]: value }));
  }

  refresh(): void {
    this.loadScores();
    this.loadStats();
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
