import { Component, ChangeDetectionStrategy, inject, signal, OnInit, OnDestroy } from '@angular/core';
import { DatePipe } from '@angular/common';
import { NgIcon } from '@ng-icons/core';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import {
  CardComponent, BadgeComponent, ButtonComponent, SelectComponent,
  InputComponent, PaginatorComponent, EmptyStateComponent, type SelectOption,
} from '@ui';
import { AnimatedCounterComponent } from '@ui/animated-counter/animated-counter.component';
import { StrikesApi } from '@core/api/strikes.api';
import { ToastService } from '@core/services/toast.service';
import { ConfirmService } from '@core/services/confirm.service';
import { DownloadItemStrikes, StrikeFilter } from '@core/models/strike.models';

@Component({
  selector: 'app-strikes',
  standalone: true,
  imports: [
    DatePipe,
    NgIcon,
    PageHeaderComponent,
    CardComponent,
    BadgeComponent,
    ButtonComponent,
    SelectComponent,
    InputComponent,
    PaginatorComponent,
    EmptyStateComponent,
    AnimatedCounterComponent,
  ],
  templateUrl: './strikes.component.html',
  styleUrl: './strikes.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StrikesComponent implements OnInit, OnDestroy {
  private readonly strikesApi = inject(StrikesApi);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmService);
  private pollTimer: ReturnType<typeof setInterval> | null = null;

  readonly items = signal<DownloadItemStrikes[]>([]);
  readonly totalRecords = signal(0);
  readonly loading = signal(false);
  readonly expandedId = signal<string | null>(null);

  readonly currentPage = signal(1);
  readonly pageSize = signal(50);
  readonly selectedType = signal<unknown>('');
  readonly searchQuery = signal('');

  readonly typeOptions = signal<SelectOption[]>([{ label: 'All Types', value: '' }]);

  ngOnInit(): void {
    this.loadStrikeTypes();
    this.loadStrikes();
    this.pollTimer = setInterval(() => this.loadStrikes(), 10_000);
  }

  ngOnDestroy(): void {
    if (this.pollTimer) {
      clearInterval(this.pollTimer);
    }
  }

  loadStrikes(): void {
    const filter: StrikeFilter = {
      page: this.currentPage(),
      pageSize: this.pageSize(),
    };
    const type = this.selectedType() as string;
    const search = this.searchQuery();

    if (type) filter.type = type;
    if (search) filter.search = search;

    this.loading.set(true);
    this.strikesApi.getStrikes(filter).subscribe({
      next: (result) => {
        this.items.set(result.items);
        this.totalRecords.set(result.totalCount);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.error('Failed to load strikes');
      },
    });
  }

  private loadStrikeTypes(): void {
    this.strikesApi.getStrikeTypes().subscribe({
      next: (types) => {
        this.typeOptions.set([
          { label: 'All Types', value: '' },
          ...types.map((t) => ({ label: this.formatStrikeType(t), value: t })),
        ]);
      },
    });
  }

  onFilterChange(): void {
    this.currentPage.set(1);
    this.loadStrikes();
  }

  onPageChange(page: number): void {
    this.currentPage.set(page);
    this.loadStrikes();
  }

  toggleExpand(itemId: string): void {
    this.expandedId.update((current) => (current === itemId ? null : itemId));
  }

  async deleteItemStrikes(item: DownloadItemStrikes): Promise<void> {
    const confirmed = await this.confirm.confirm({
      title: 'Delete Strikes',
      message: `Delete all ${item.totalStrikes} strike(s) for "${item.title}"? This action cannot be undone.`,
      confirmLabel: 'Delete',
      destructive: true,
    });

    if (!confirmed) return;

    this.strikesApi.deleteStrikesForItem(item.downloadItemId).subscribe({
      next: () => {
        this.toast.success(`Strikes deleted for "${item.title}"`);
        this.loadStrikes();
      },
      error: () => this.toast.error('Failed to delete strikes'),
    });
  }

  refresh(): void {
    this.loadStrikes();
  }

  // Helpers
  strikeTypeSeverity(type: string): 'error' | 'warning' | 'info' | 'default' {
    const t = type.toLowerCase();
    if (t === 'failedimport') return 'error';
    if (t === 'stalled') return 'warning';
    if (t === 'slowspeed' || t === 'slowtime') return 'info';
    return 'default';
  }

  formatStrikeType(type: string): string {
    return type.replace(/([A-Z])/g, ' $1').trim();
  }

  formatBytes(bytes: number | null): string {
    if (bytes === null || bytes === undefined) return '-';
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
  }

  strikeTypeEntries(strikesByType: Record<string, number>): { type: string; count: number }[] {
    return Object.entries(strikesByType).map(([type, count]) => ({ type, count }));
  }

}
