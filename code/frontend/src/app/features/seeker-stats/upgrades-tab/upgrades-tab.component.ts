import { Component, ChangeDetectionStrategy, inject, signal, computed, effect, untracked, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { NgIcon } from '@ng-icons/core';
import {
  CardComponent, BadgeComponent, ButtonComponent, SelectComponent,
  InputComponent, PaginatorComponent, EmptyStateComponent,
  DrawerComponent,
} from '@ui';
import type { SelectOption } from '@ui';
import { AnimatedCounterComponent } from '@ui/animated-counter/animated-counter.component';
import { CfScoreApi, CfScoreUpgrade, CfUpgradesSortBy, SortDirection } from '@core/api/cf-score.api';
import { AppHubService } from '@core/realtime/app-hub.service';
import { ToastService } from '@core/services/toast.service';
import { PaginationService } from '@core/services/pagination.service';
import { StickyAwareDirective } from '@core/directives/sticky-aware.directive';

const DEFAULT_SORT_BY = CfUpgradesSortBy.UpgradedAt;
const DEFAULT_SORT_DIRECTION = SortDirection.Desc;

interface AdvancedFilters {
  instanceId: string;
  timeRange: string;
}

const EMPTY_FILTERS: AdvancedFilters = {
  instanceId: '',
  timeRange: '30',
};

@Component({
  selector: 'app-upgrades-tab',
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
    DrawerComponent,
    StickyAwareDirective,
  ],
  templateUrl: './upgrades-tab.component.html',
  styleUrl: './upgrades-tab.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UpgradesTabComponent implements OnInit {
  private static readonly PAGE_SIZE_KEY = 'cleanuparr-page-size-seeker-upgrades';

  private readonly api = inject(CfScoreApi);
  private readonly hub = inject(AppHubService);
  private readonly toast = inject(ToastService);
  private readonly pagination = inject(PaginationService);
  private initialLoad = true;
  private latestLoadToken = 0;

  readonly upgrades = signal<CfScoreUpgrade[]>([]);
  readonly totalRecords = signal(0);
  readonly currentPage = signal(1);
  readonly pageSize = signal(this.pagination.getPageSize(UpgradesTabComponent.PAGE_SIZE_KEY, 50));
  readonly loading = signal(false);

  readonly searchQuery = signal('');
  readonly selectedInstanceId = signal<string>('');
  readonly instanceOptions = signal<SelectOption[]>([]);

  readonly sortBy = signal<CfUpgradesSortBy>(DEFAULT_SORT_BY);
  readonly sortDirection = signal<SortDirection>(DEFAULT_SORT_DIRECTION);

  readonly applied = signal<AdvancedFilters>({ ...EMPTY_FILTERS });
  readonly draft = signal<AdvancedFilters>({ ...EMPTY_FILTERS });
  readonly drawerOpen = signal(false);

  readonly sortOptions: SelectOption[] = [
    { label: 'Upgraded At', value: CfUpgradesSortBy.UpgradedAt },
    { label: 'Title', value: CfUpgradesSortBy.Title },
    { label: 'New Score', value: CfUpgradesSortBy.NewScore },
    { label: 'Previous Score', value: CfUpgradesSortBy.PreviousScore },
    { label: 'Score Delta', value: CfUpgradesSortBy.ScoreDelta },
    { label: 'Cutoff', value: CfUpgradesSortBy.CutoffScore },
  ];

  readonly sortOrderOptions: SelectOption[] = [
    { label: 'Descending', value: SortDirection.Desc },
    { label: 'Ascending', value: SortDirection.Asc },
  ];

  readonly timeRangeOptions: SelectOption[] = [
    { label: 'Last 7 Days', value: '7' },
    { label: 'Last 30 Days', value: '30' },
    { label: 'Last 90 Days', value: '90' },
    { label: 'All Time', value: '0' },
  ];

  readonly activeFilterCount = computed(() => {
    const a = this.applied();
    let n = 0;
    if (a.instanceId) n++;
    if (a.timeRange !== EMPTY_FILTERS.timeRange) n++;
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
        this.loadUpgrades();
      });
    });
  }

  ngOnInit(): void {
    this.loadInstances();
    this.loadUpgrades();
  }

  onSearchFilterChange(): void {
    this.currentPage.set(1);
    this.loadUpgrades();
  }

  onSortByChange(value: CfUpgradesSortBy): void {
    this.sortBy.set(value);
    this.currentPage.set(1);
    this.loadUpgrades();
  }

  onSortOrderChange(value: SortDirection): void {
    this.sortDirection.set(value);
    this.currentPage.set(1);
    this.loadUpgrades();
  }

  onPageChange(page: number): void {
    this.currentPage.set(page);
    this.loadUpgrades();
  }

  readonly onPageSizeChange = this.pagination.createPageSizeHandler(
    UpgradesTabComponent.PAGE_SIZE_KEY,
    this.pageSize,
    this.currentPage,
    () => this.loadUpgrades(),
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
    this.currentPage.set(1);
    this.loadUpgrades();
  }

  updateDraft<K extends keyof AdvancedFilters>(key: K, value: AdvancedFilters[K]): void {
    this.draft.update(d => ({ ...d, [key]: value }));
  }

  refresh(): void {
    this.loadUpgrades();
  }

  itemTypeSeverity(itemType: string): 'info' | 'default' {
    return itemType === 'Radarr' || itemType === 'Sonarr' ? 'info' : 'default';
  }

  private loadInstances(): void {
    this.api.getInstances().subscribe({
      next: (result) => {
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

  private loadUpgrades(): void {
    this.loading.set(true);
    const loadToken = ++this.latestLoadToken;
    const a = this.applied();
    const days = parseInt(a.timeRange, 10);
    const instanceId = this.selectedInstanceId() || undefined;

    this.api.getRecentUpgrades({
      page: this.currentPage(),
      pageSize: this.pageSize(),
      instanceId,
      days: Number.isFinite(days) ? days : undefined,
      search: this.searchQuery() || undefined,
      sortBy: this.sortBy(),
      sortDirection: this.sortDirection(),
    }).subscribe({
      next: (result) => {
        if (loadToken !== this.latestLoadToken) return;
        this.upgrades.set(result.items);
        this.totalRecords.set(result.totalCount);
        this.loading.set(false);
      },
      error: () => {
        if (loadToken !== this.latestLoadToken) return;
        this.loading.set(false);
        this.toast.error('Failed to load upgrades');
      },
    });
  }
}
