import { Component, ChangeDetectionStrategy, inject, signal, computed, effect, OnInit, OnDestroy } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
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
import { PaginationService } from '@core/services/pagination.service';
import { StickyAwareDirective } from '@core/directives/sticky-aware.directive';
import { DownloadItemStrikes, StrikeFilter } from '@core/models/strike.models';
import { PaginatedResult } from '@core/models/pagination.model';

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
    StickyAwareDirective,
  ],
  templateUrl: './strikes.component.html',
  styleUrl: './strikes.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StrikesComponent implements OnInit, OnDestroy {
  private static readonly PAGE_SIZE_KEY = 'cleanuparr-page-size-strikes';

  private readonly strikesApi = inject(StrikesApi);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmService);
  private readonly pagination = inject(PaginationService);
  private pollTimer: ReturnType<typeof setInterval> | null = null;

  readonly expandedId = signal<string | null>(null);

  readonly currentPage = signal(1);
  readonly pageSize = signal(this.pagination.getPageSize(StrikesComponent.PAGE_SIZE_KEY, 50));
  readonly selectedType = signal<unknown>('');
  readonly searchQuery = signal('');

  private readonly strikeFilter = computed<StrikeFilter>(() => {
    const filter: StrikeFilter = {
      page: this.currentPage(),
      pageSize: this.pageSize(),
    };
    const type = this.selectedType() as string;
    const search = this.searchQuery();
    if (type) {
      filter.type = type;
    }
    if (search) {
      filter.search = search;
    }
    return filter;
  });

  private readonly strikesResource = rxResource({
    params: () => this.strikeFilter(),
    stream: ({ params }) => this.strikesApi.getStrikes(params),
    defaultValue: { items: [], page: 1, pageSize: 50, totalCount: 0, totalPages: 0 } as PaginatedResult<DownloadItemStrikes>,
  });

  private readonly strikeTypesResource = rxResource({
    stream: () => this.strikesApi.getStrikeTypes(),
    defaultValue: [] as string[],
  });

  readonly items = computed(() => this.strikesResource.value().items);
  readonly totalRecords = computed(() => this.strikesResource.value().totalCount);
  readonly typeOptions = computed<SelectOption[]>(() => [
    { label: 'All Types', value: '' },
    ...this.strikeTypesResource.value().map((t) => ({ label: this.formatStrikeType(t), value: t })),
  ]);

  constructor() {
    effect(() => {
      if (this.strikesResource.error()) {
        this.toast.error('Failed to load strikes');
      }
    });
  }

  ngOnInit(): void {
    this.pollTimer = setInterval(() => this.strikesResource.reload(), 10_000);
  }

  ngOnDestroy(): void {
    if (this.pollTimer) {
      clearInterval(this.pollTimer);
    }
  }

  onFilterChange(): void {
    this.currentPage.set(1);
  }

  onPageChange(page: number): void {
    this.currentPage.set(page);
  }

  readonly onPageSizeChange = this.pagination.createPageSizeHandler(
    StrikesComponent.PAGE_SIZE_KEY,
    this.pageSize,
    this.currentPage,
    () => this.strikesResource.reload(),
  );

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
        this.strikesResource.reload();
      },
      error: () => this.toast.error('Failed to delete strikes'),
    });
  }

  refresh(): void {
    this.strikesResource.reload();
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
