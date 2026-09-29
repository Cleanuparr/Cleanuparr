import { Component, ChangeDetectionStrategy, inject, signal, computed, effect } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { NgIcon } from '@ng-icons/core';
import {
  CardComponent, BadgeComponent, ButtonComponent, SelectComponent,
  InputComponent, PaginatorComponent, EmptyStateComponent,
  DrawerComponent,
} from '@ui';
import type { SelectOption } from '@ui';
import { AnimatedCounterComponent } from '@ui/animated-counter/animated-counter.component';
import {
  CfScoreApi, CfScoreUpgradesResponse, CfScoreUpgradesQuery,
  CfScoreInstance, CfUpgradesSortBy, SortDirection,
} from '@core/api/cf-score.api';
import { AppHubService } from '@core/realtime/app-hub.service';
import { ToastService } from '@core/services/toast.service';
import { PaginationService, PAGE_SIZE_STORAGE_KEYS } from '@core/services/pagination.service';
import { StickyAwareDirective } from '@core/directives/sticky-aware.directive';
import { instanceTypeHighlight } from '@shared/utils/instance-display.util';
import { createFilterDrawer } from '@shared/utils/filter-drawer.util';

const DEFAULT_SORT_BY = CfUpgradesSortBy.UpgradedAt;
const DEFAULT_SORT_DIRECTION = SortDirection.Desc;

interface AdvancedFilters extends Record<string, unknown> {
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
export class UpgradesTabComponent {
  private readonly api = inject(CfScoreApi);
  private readonly hub = inject(AppHubService);
  private readonly toast = inject(ToastService);
  private readonly pagination = inject(PaginationService);
  private initialLoad = true;

  readonly currentPage = signal(1);
  readonly pageSize = signal(
    this.pagination.getPageSize(PAGE_SIZE_STORAGE_KEYS.seekerUpgrades, PaginationService.DEFAULT_PAGE_SIZE),
  );

  readonly searchQuery = signal('');
  readonly selectedInstanceId = signal<string>('');

  readonly sortBy = signal<CfUpgradesSortBy>(DEFAULT_SORT_BY);
  readonly sortDirection = signal<SortDirection>(DEFAULT_SORT_DIRECTION);

  private readonly filters = createFilterDrawer<AdvancedFilters>(EMPTY_FILTERS);
  readonly applied = this.filters.applied;
  readonly draft = this.filters.draft;
  readonly drawerOpen = this.filters.drawerOpen;
  readonly activeFilterCount = this.filters.activeCount;

  private readonly upgradesParams = computed<CfScoreUpgradesQuery>(() => {
    const a = this.applied();
    const days = parseInt(a.timeRange, 10);
    return {
      page: this.currentPage(),
      pageSize: this.pageSize(),
      instanceId: this.selectedInstanceId() || undefined,
      days: Number.isFinite(days) ? days : undefined,
      search: this.searchQuery() || undefined,
      sortBy: this.sortBy(),
      sortDirection: this.sortDirection(),
    };
  });

  private readonly upgradesResource = rxResource({
    params: () => this.upgradesParams(),
    stream: ({ params }) => this.api.getRecentUpgrades(params),
    defaultValue: { items: [], page: 1, pageSize: 50, totalCount: 0, totalPages: 0 } as CfScoreUpgradesResponse,
  });

  private readonly instancesResource = rxResource({
    stream: () => this.api.getInstances(),
    defaultValue: { instances: [] as CfScoreInstance[] },
  });

  readonly upgrades = computed(() => this.upgradesResource.hasValue() ? this.upgradesResource.value().items : []);
  readonly totalRecords = computed(() => this.upgradesResource.hasValue() ? this.upgradesResource.value().totalCount : 0);
  readonly instanceOptions = computed<SelectOption[]>(() => [
    { label: 'All Instances', value: '' },
    ...(this.instancesResource.hasValue() ? this.instancesResource.value().instances : []).map((i) => ({ label: `${i.name} (${i.itemType})`, value: i.id })),
  ]);

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

  constructor() {
    effect(() => {
      this.hub.cfScoresVersion();
      if (this.initialLoad) {
        this.initialLoad = false;
        return;
      }
      this.upgradesResource.reload();
    });
    effect(() => {
      const err = this.upgradesResource.error();
      if (err) {
        this.toast.error(`Failed to load upgrades: ${err.message}`);
      }
    });
    effect(() => {
      const err = this.instancesResource.error();
      if (err) {
        this.toast.error(`Failed to load instances: ${err.message}`);
      }
    });
  }

  onSearchFilterChange(): void {
    this.currentPage.set(1);
  }

  onSortByChange(value: CfUpgradesSortBy): void {
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
    PAGE_SIZE_STORAGE_KEYS.seekerUpgrades,
    this.pageSize,
    this.currentPage,
  );

  openFilters(): void {
    this.filters.open();
    this.filters.updateDraft('instanceId', this.selectedInstanceId());
  }

  resetFilters(): void {
    this.filters.reset();
  }

  applyFilters(): void {
    this.filters.apply();
    this.selectedInstanceId.set(this.filters.applied().instanceId);
    this.currentPage.set(1);
  }

  updateDraft<K extends keyof AdvancedFilters>(key: K, value: AdvancedFilters[K]): void {
    this.filters.updateDraft(key, value);
  }

  refresh(): void {
    this.upgradesResource.reload();
  }

  readonly itemTypeSeverity = instanceTypeHighlight;
}
