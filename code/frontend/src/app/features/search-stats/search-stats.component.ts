import { Component, ChangeDetectionStrategy, inject, signal, computed, effect, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { NgIcon } from '@ng-icons/core';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import {
  CardComponent, BadgeComponent, ButtonComponent, SelectComponent,
  PaginatorComponent, EmptyStateComponent, TabsComponent, TooltipComponent,
} from '@ui';
import type { Tab, SelectOption } from '@ui';
import type { BadgeSeverity } from '@ui/badge/badge.component';
import { AnimatedCounterComponent } from '@ui/animated-counter/animated-counter.component';
import { SearchStatsApi } from '@core/api/search-stats.api';
import type { SearchStatsSummary, SearchHistoryEntry, SearchEvent, InstanceSearchStat } from '@core/models/search-stats.models';
import { SeekerSearchType } from '@core/models/search-stats.models';
import { AppHubService } from '@core/realtime/app-hub.service';
import { ToastService } from '@core/services/toast.service';

type TabId = 'events' | 'items';
type ItemsSortBy = 'lastSearched' | 'searchCount';
type CycleFilter = 'current' | 'all';

@Component({
  selector: 'app-search-stats',
  standalone: true,
  imports: [
    DatePipe,
    NgIcon,
    PageHeaderComponent,
    CardComponent,
    BadgeComponent,
    ButtonComponent,
    SelectComponent,
    PaginatorComponent,
    EmptyStateComponent,
    TabsComponent,
    AnimatedCounterComponent,
    TooltipComponent,
  ],
  templateUrl: './search-stats.component.html',
  styleUrl: './search-stats.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SearchStatsComponent implements OnInit {
  private readonly api = inject(SearchStatsApi);
  private readonly hub = inject(AppHubService);
  private readonly toast = inject(ToastService);
  private initialLoad = true;

  readonly summary = signal<SearchStatsSummary | null>(null);
  readonly loading = signal(false);

  readonly sortedInstanceStats = computed(() =>
    [...(this.summary()?.perInstanceStats ?? [])].sort((a, b) => {
      const typeCompare = a.instanceType.localeCompare(b.instanceType);
      return typeCompare !== 0 ? typeCompare : a.instanceName.localeCompare(b.instanceName);
    })
  );

  // Tabs
  readonly activeTab = signal<string>('events');
  readonly tabs: Tab[] = [
    { id: 'events', label: 'Events' },
    { id: 'items', label: 'Items' },
  ];

  // Instance filter
  readonly selectedInstanceId = signal<string>('');
  readonly instanceOptions = signal<SelectOption[]>([]);

  // Cycle filter
  readonly cycleFilter = signal<CycleFilter>('current');
  readonly cycleFilterOptions: SelectOption[] = [
    { label: 'Current Cycle', value: 'current' },
    { label: 'All Time', value: 'all' },
  ];

  // Events tab
  readonly events = signal<SearchEvent[]>([]);
  readonly eventsTotalRecords = signal(0);
  readonly eventsPage = signal(1);

  // Items tab
  readonly items = signal<SearchHistoryEntry[]>([]);
  readonly itemsTotalRecords = signal(0);
  readonly itemsPage = signal(1);
  readonly itemsSortBy = signal<ItemsSortBy>('lastSearched');

  readonly sortOptions: SelectOption[] = [
    { label: 'Last Searched', value: 'lastSearched' },
    { label: 'Most Searched', value: 'searchCount' },
  ];

  readonly pageSize = signal(50);

  // Item expand
  readonly expandedItemId = signal<string | null>(null);
  readonly detailEntries = signal<SearchEvent[]>([]);
  readonly detailLoading = signal(false);

  constructor() {
    effect(() => {
      this.hub.searchStatsVersion(); // subscribe to changes
      if (this.initialLoad) {
        this.initialLoad = false;
        return;
      }
      this.loadSummary();
      this.loadActiveTab();
    });
  }

  ngOnInit(): void {
    this.loadSummary();
    this.loadActiveTab();
  }

  onTabChange(tabId: string): void {
    this.activeTab.set(tabId);
    this.loadActiveTab();
  }

  onInstanceFilterChange(value: string): void {
    this.selectedInstanceId.set(value);
    if (!value) {
      this.cycleFilter.set('all');
    }
    this.eventsPage.set(1);
    this.itemsPage.set(1);
    this.loadActiveTab();
  }

  onCycleFilterChange(value: string): void {
    this.cycleFilter.set(value as CycleFilter);
    this.eventsPage.set(1);
    this.loadEvents();
  }

  onEventsPageChange(page: number): void {
    this.eventsPage.set(page);
    this.loadEvents();
  }

  onItemsPageChange(page: number): void {
    this.itemsPage.set(page);
    this.loadItems();
  }

  onItemsSortChange(value: string): void {
    this.itemsSortBy.set(value as ItemsSortBy);
    this.itemsPage.set(1);
    this.loadItems();
  }

  refresh(): void {
    this.loadSummary();
    this.loadActiveTab();
  }

  toggleItemExpand(item: SearchHistoryEntry): void {
    const id = item.id;
    if (this.expandedItemId() === id) {
      this.expandedItemId.set(null);
      this.detailEntries.set([]);
      return;
    }

    this.expandedItemId.set(id);
    this.detailLoading.set(true);
    this.detailEntries.set([]);

    this.api.getItemDetail(item.arrInstanceId, item.externalItemId, item.seasonNumber).subscribe({
      next: (res) => {
        this.detailEntries.set(res.entries);
        this.detailLoading.set(false);
      },
      error: () => {
        this.detailLoading.set(false);
        this.toast.error('Failed to load item detail');
      },
    });
  }

  searchTypeSeverity(type: SeekerSearchType): 'info' | 'warning' {
    return type === SeekerSearchType.Replacement ? 'warning' : 'info';
  }

  instanceTypeSeverity(type: string): BadgeSeverity {
    if (type === 'Radarr') return 'warning';
    if (type === 'Sonarr') return 'info';
    return 'default';
  }

  itemDisplayName(item: { itemTitle: string; externalItemId: number }): string {
    return item.itemTitle || `Item #${item.externalItemId}`;
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

  formatGrabbedItems(items: unknown[]): string {
    return items.map((i: any) => i.Title || i.title || 'Unknown').join(', ');
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

  private loadActiveTab(): void {
    const tab = this.activeTab() as TabId;
    switch (tab) {
      case 'events':
        this.loadEvents();
        break;
      case 'items':
        this.loadItems();
        break;
    }
  }

  private loadEvents(): void {
    this.loading.set(true);
    const instanceId = this.selectedInstanceId() || undefined;
    let cycleId: string | undefined;

    if (this.cycleFilter() === 'current' && instanceId) {
      const instance = this.summary()?.perInstanceStats.find(s => s.instanceId === instanceId);
      cycleId = instance?.currentCycleId ?? undefined;
    }

    this.api.getEvents(this.eventsPage(), this.pageSize(), instanceId, cycleId).subscribe({
      next: (result) => {
        this.events.set(result.items);
        this.eventsTotalRecords.set(result.totalCount);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.error('Failed to load search events');
      },
    });
  }

  private loadItems(): void {
    this.loading.set(true);
    this.expandedItemId.set(null);
    this.detailEntries.set([]);
    const instanceId = this.selectedInstanceId() || undefined;
    this.api.getHistory(this.itemsPage(), this.pageSize(), instanceId, this.itemsSortBy()).subscribe({
      next: (result) => {
        this.items.set(result.items);
        this.itemsTotalRecords.set(result.totalCount);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.error('Failed to load items');
      },
    });
  }
}
