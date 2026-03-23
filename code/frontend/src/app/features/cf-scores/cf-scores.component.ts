import { Component, ChangeDetectionStrategy, inject, signal, OnInit, OnDestroy } from '@angular/core';
import { DatePipe } from '@angular/common';
import { NgIcon } from '@ng-icons/core';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import {
  CardComponent, BadgeComponent, ButtonComponent, InputComponent,
  PaginatorComponent, EmptyStateComponent, SelectComponent, ToggleComponent,
} from '@ui';
import type { SelectOption } from '@ui';
import { AnimatedCounterComponent } from '@ui/animated-counter/animated-counter.component';
import {
  CfScoreApi, CfScoreEntry, CfScoreStats, CfScoreHistoryEntry,
} from '@core/api/cf-score.api';
import { ToastService } from '@core/services/toast.service';

const POLL_INTERVAL_MS = 10_000;

@Component({
  selector: 'app-cf-scores',
  standalone: true,
  imports: [
    DatePipe,
    NgIcon,
    PageHeaderComponent,
    CardComponent,
    BadgeComponent,
    ButtonComponent,
    InputComponent,
    SelectComponent,
    ToggleComponent,
    PaginatorComponent,
    EmptyStateComponent,
    AnimatedCounterComponent,
  ],
  templateUrl: './cf-scores.component.html',
  styleUrl: './cf-scores.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CfScoresComponent implements OnInit, OnDestroy {
  private readonly api = inject(CfScoreApi);
  private readonly toast = inject(ToastService);
  private pollTimer: ReturnType<typeof setInterval> | null = null;

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

  readonly expandedId = signal<string | null>(null);
  readonly historyEntries = signal<CfScoreHistoryEntry[]>([]);
  readonly historyLoading = signal(false);

  ngOnInit(): void {
    this.loadInstances();
    this.loadScores();
    this.loadStats();
    this.pollTimer = setInterval(() => {
      this.loadScores();
      this.loadStats();
    }, POLL_INTERVAL_MS);
  }

  ngOnDestroy(): void {
    if (this.pollTimer) {
      clearInterval(this.pollTimer);
    }
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
