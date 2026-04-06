import { Component, ChangeDetectionStrategy, inject, signal, computed, effect, untracked, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { NgIcon } from '@ng-icons/core';
import {
  CardComponent, BadgeComponent, ButtonComponent, SelectComponent,
  InputComponent, PaginatorComponent, EmptyStateComponent, TooltipComponent,
} from '@ui';
import type { SelectOption } from '@ui';
import type { BadgeSeverity } from '@ui/badge/badge.component';
import { AnimatedCounterComponent } from '@ui/animated-counter/animated-counter.component';
import { SearchStatsApi } from '@core/api/search-stats.api';
import type { SearchStatsSummary, SearchEvent, InstanceSearchStat } from '@core/models/search-stats.models';
import { SeekerSearchType, SeekerSearchReason } from '@core/models/search-stats.models';
import { AppHubService } from '@core/realtime/app-hub.service';
import { ToastService } from '@core/services/toast.service';

type CycleFilter = 'current' | 'all';

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
  ],
  templateUrl: './searches-tab.component.html',
  styleUrl: './searches-tab.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SearchesTabComponent implements OnInit {
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

  // Instance filter
  readonly selectedInstanceId = signal<string>('');
  readonly instanceOptions = signal<SelectOption[]>([]);

  // Cycle filter
  readonly cycleFilter = signal<CycleFilter>('current');
  readonly cycleFilterOptions: SelectOption[] = [
    { label: 'Current Cycle', value: 'current' },
    { label: 'All Time', value: 'all' },
  ];

  // Search filter
  readonly searchQuery = signal('');

  // Events
  readonly events = signal<SearchEvent[]>([]);
  readonly eventsTotalRecords = signal(0);
  readonly eventsPage = signal(1);
  readonly pageSize = signal(50);

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

  onInstanceFilterChange(value: string): void {
    this.selectedInstanceId.set(value);
    if (!value) {
      this.cycleFilter.set('all');
    }
    this.eventsPage.set(1);
    this.loadEvents();
  }

  onCycleFilterChange(value: string): void {
    this.cycleFilter.set(value as CycleFilter);
    this.eventsPage.set(1);
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
    const instanceId = this.selectedInstanceId() || undefined;
    const search = this.searchQuery() || undefined;
    let cycleId: string | undefined;

    if (this.cycleFilter() === 'current' && instanceId) {
      const instance = this.summary()?.perInstanceStats.find(s => s.instanceId === instanceId);
      cycleId = instance?.currentCycleId ?? undefined;
    }

    this.api.getEvents(this.eventsPage(), this.pageSize(), instanceId, cycleId, search).subscribe({
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
}
