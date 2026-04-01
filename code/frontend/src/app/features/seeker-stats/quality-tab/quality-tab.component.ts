import { Component, ChangeDetectionStrategy, inject, signal, computed, effect, untracked, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { NgIcon } from '@ng-icons/core';
import {
  CardComponent, BadgeComponent, ButtonComponent, InputComponent,
  PaginatorComponent, EmptyStateComponent, SelectComponent, ToggleComponent,
  TooltipComponent,
} from '@ui';
import type { SelectOption } from '@ui';
import { AnimatedCounterComponent } from '@ui/animated-counter/animated-counter.component';
import {
  CfScoreApi, CfScoreEntry, CfScoreStats, CfScoreHistoryEntry,
} from '@core/api/cf-score.api';
import { AppHubService } from '@core/realtime/app-hub.service';
import { ToastService } from '@core/services/toast.service';

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
    ToggleComponent,
    PaginatorComponent,
    EmptyStateComponent,
    AnimatedCounterComponent,
    TooltipComponent,
  ],
  templateUrl: './quality-tab.component.html',
  styleUrl: './quality-tab.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class QualityTabComponent implements OnInit {
  private readonly api = inject(CfScoreApi);
  private readonly hub = inject(AppHubService);
  private readonly toast = inject(ToastService);
  private initialLoad = true;

  readonly items = signal<CfScoreEntry[]>([]);
  readonly stats = signal<CfScoreStats | null>(null);
  readonly totalRecords = signal(0);
  readonly loading = signal(false);

  readonly currentPage = signal(1);
  readonly pageSize = signal(50);
  readonly searchQuery = signal('');
  readonly selectedInstanceId = signal<string>('');
  readonly instanceOptions = signal<SelectOption[]>([]);

  readonly sortBy = signal<string>('title');
  readonly hideMet = signal(false);
  readonly sortOptions: SelectOption[] = [
    { label: 'Title', value: 'title' },
    { label: 'Last Synced', value: 'date' },
  ];

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
    this.api.getScores(this.currentPage(), this.pageSize(), this.searchQuery() || undefined, this.selectedInstanceId() || undefined, this.sortBy(), this.hideMet()).subscribe({
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

  onSortChange(value: string): void {
    this.sortBy.set(value);
    this.currentPage.set(1);
    this.loadScores();
  }

  onHideMetChange(value: boolean): void {
    this.hideMet.set(value);
    this.currentPage.set(1);
    this.loadScores();
  }

  onPageChange(page: number): void {
    this.currentPage.set(page);
    this.loadScores();
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
