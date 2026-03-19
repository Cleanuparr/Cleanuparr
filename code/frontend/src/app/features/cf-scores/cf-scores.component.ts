import { Component, ChangeDetectionStrategy, inject, signal, OnInit, OnDestroy } from '@angular/core';
import { DatePipe } from '@angular/common';
import { NgIcon } from '@ng-icons/core';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import {
  CardComponent, BadgeComponent, ButtonComponent, InputComponent,
  PaginatorComponent, EmptyStateComponent,
} from '@ui';
import { AnimatedCounterComponent } from '@ui/animated-counter/animated-counter.component';
import {
  CfScoreApi, CfScoreEntry, CfScoreStats, CfScoreHistoryEntry,
} from '@core/api/cf-score.api';
import { ToastService } from '@core/services/toast.service';

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

  readonly expandedId = signal<string | null>(null);
  readonly historyEntries = signal<CfScoreHistoryEntry[]>([]);
  readonly historyLoading = signal(false);

  ngOnInit(): void {
    this.loadScores();
    this.loadStats();
    this.pollTimer = setInterval(() => {
      this.loadScores();
      this.loadStats();
    }, 10_000);
  }

  ngOnDestroy(): void {
    if (this.pollTimer) {
      clearInterval(this.pollTimer);
    }
  }

  loadScores(): void {
    this.loading.set(true);
    this.api.getScores(this.currentPage(), this.pageSize(), this.searchQuery() || undefined).subscribe({
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

  private loadStats(): void {
    this.api.getStats().subscribe({
      next: (stats) => this.stats.set(stats),
    });
  }

  onFilterChange(): void {
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
