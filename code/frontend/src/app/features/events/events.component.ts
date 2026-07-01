import { Component, ChangeDetectionStrategy, inject, signal, computed, effect, OnInit, OnDestroy } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { NgIcon } from '@ng-icons/core';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import {
  CardComponent, BadgeComponent, ButtonComponent, SelectComponent,
  InputComponent, PaginatorComponent, EmptyStateComponent, type SelectOption,
} from '@ui';
import { EventsApi } from '@core/api/events.api';
import { ToastService } from '@core/services/toast.service';
import { PaginationService } from '@core/services/pagination.service';
import { StickyAwareDirective } from '@core/directives/sticky-aware.directive';
import { AnimatedCounterComponent } from '@ui/animated-counter/animated-counter.component';
import { AppEvent, EventFilter } from '@core/models/event.models';
import { PaginatedResult } from '@core/models/pagination.model';

@Component({
  selector: 'app-events',
  standalone: true,
  imports: [
    DatePipe,
    FormsModule,
    RouterLink,
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
  templateUrl: './events.component.html',
  styleUrl: './events.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EventsComponent implements OnInit, OnDestroy {
  private static readonly PAGE_SIZE_KEY = 'cleanuparr-page-size-events';

  private readonly eventsApi = inject(EventsApi);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private readonly pagination = inject(PaginationService);
  private pollTimer: ReturnType<typeof setInterval> | null = null;

  readonly expandedId = signal<string | null>(null);
  readonly showExportMenu = signal(false);
  readonly selectedJobRunId = signal<string | null>(null);

  readonly currentPage = signal(1);
  readonly pageSize = signal(this.pagination.getPageSize(EventsComponent.PAGE_SIZE_KEY, 50));
  readonly selectedSeverity = signal<unknown>('');
  readonly selectedType = signal<unknown>('');
  readonly searchQuery = signal('');
  readonly fromDate = signal('');
  readonly toDate = signal('');

  private readonly eventFilter = computed<EventFilter>(() => {
    const filter: EventFilter = {
      page: this.currentPage(),
      pageSize: this.pageSize(),
    };
    const severity = this.selectedSeverity() as string;
    const type = this.selectedType() as string;
    const search = this.searchQuery();
    const from = this.fromDate();
    const to = this.toDate();
    const jobRunId = this.selectedJobRunId();

    if (severity) {
      filter.severity = severity;
    }
    if (type) {
      filter.eventType = type;
    }
    if (search) {
      filter.search = search;
    }
    if (from) {
      filter.fromDate = from;
    }
    if (to) {
      filter.toDate = to;
    }
    if (jobRunId) {
      filter.jobRunId = jobRunId;
    }
    return filter;
  });

  private readonly eventsResource = rxResource({
    params: () => this.eventFilter(),
    stream: ({ params }) => this.eventsApi.getEvents(params),
    defaultValue: { items: [], page: 1, pageSize: 50, totalCount: 0, totalPages: 0 } as PaginatedResult<AppEvent>,
  });

  private readonly severitiesResource = rxResource({
    stream: () => this.eventsApi.getSeverities(),
    defaultValue: [] as string[],
  });

  private readonly eventTypesResource = rxResource({
    stream: () => this.eventsApi.getEventTypes(),
    defaultValue: [] as string[],
  });

  readonly events = computed(() => this.eventsResource.value().items);
  readonly totalRecords = computed(() => this.eventsResource.value().totalCount);
  readonly severityOptions = computed<SelectOption[]>(() => [
    { label: 'All Severities', value: '' },
    ...this.severitiesResource.value().map((s) => ({ label: s, value: s })),
  ]);
  readonly typeOptions = computed<SelectOption[]>(() => [
    { label: 'All Types', value: '' },
    ...this.eventTypesResource.value().map((t) => ({ label: this.formatEventType(t), value: t })),
  ]);

  constructor() {
    effect(() => {
      if (this.eventsResource.error()) {
        this.toast.error('Failed to load events');
      }
    });
  }

  ngOnInit(): void {
    this.pollTimer = setInterval(() => this.eventsResource.reload(), 10_000);
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
    EventsComponent.PAGE_SIZE_KEY,
    this.pageSize,
    this.currentPage,
  );

  isExpandable(event: AppEvent): boolean {
    return !!(event.data || event.trackingId || event.instanceType || event.downloadClientType || event.jobRunId);
  }

  toggleExpand(eventId: string): void {
    this.expandedId.update((current) => (current === eventId ? null : eventId));
  }

  copyEvent(event: AppEvent): void {
    const text = `[${event.timestamp}] [${event.severity}] ${event.eventType}: ${event.message}`;
    navigator.clipboard.writeText(text);
    this.toast.success('Event copied');
  }

  refresh(): void {
    this.eventsResource.reload();
  }

  filterByJobRunId(runId: string): void {
    this.selectedJobRunId.set(runId);
    this.currentPage.set(1);
  }

  clearJobRunFilter(): void {
    this.selectedJobRunId.set(null);
    this.currentPage.set(1);
  }

  viewLogsForJobRun(runId: string): void {
    this.router.navigate(['/logs'], { queryParams: { jobRunId: runId } });
  }

  exportEvents(format: 'json' | 'csv' | 'text'): void {
    this.showExportMenu.set(false);
    const events = this.events();
    let content: string;
    let mimeType: string;
    let ext: string;

    switch (format) {
      case 'json':
        content = JSON.stringify(events, null, 2);
        mimeType = 'application/json';
        ext = 'json';
        break;
      case 'csv': {
        const header = 'Timestamp,Severity,EventType,Message,Data,TrackingId,JobRunId,InstanceType,InstanceUrl,DownloadClientType,DownloadClientName';
        const rows = events.map((e) =>
          [e.timestamp, e.severity, e.eventType, `"${(e.message ?? '').replace(/"/g, '""')}"`, `"${(e.data ?? '').replace(/"/g, '""')}"`, e.trackingId ?? '', e.jobRunId ?? '', e.instanceType ?? '', e.instanceUrl ?? '', e.downloadClientType ?? '', e.downloadClientName ?? ''].join(',')
        );
        content = [header, ...rows].join('\n');
        mimeType = 'text/csv';
        ext = 'csv';
        break;
      }
      case 'text':
        content = events
          .map((e) => `[${e.timestamp}] [${e.severity}] ${e.eventType}: ${e.message}`)
          .join('\n');
        mimeType = 'text/plain';
        ext = 'txt';
        break;
    }

    const blob = new Blob([content], { type: mimeType });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `cleanuparr-events.${ext}`;
    a.click();
    URL.revokeObjectURL(url);
    this.toast.success(`Events exported as ${format.toUpperCase()}`);
  }

  // Helpers
  eventTypeSeverity(eventType: string): 'error' | 'warning' | 'info' | 'success' | 'default' {
    const t = eventType.toLowerCase();
    if (t === 'failedimportstrike' || t === 'queueitemdeleted') return 'error';
    if (t === 'stalledstrike' || t === 'downloadmarkedfordeletion') return 'warning';
    if (t === 'downloadcleaned') return 'success';
    if (t.includes('strike') || t === 'categorychanged') return 'info';
    return 'default';
  }

  eventSeverity(severity: string): 'error' | 'warning' | 'info' | 'default' {
    const s = severity.toLowerCase();
    if (s === 'error') return 'error';
    if (s === 'warning' || s === 'important') return 'warning';
    if (s === 'information' || s === 'info') return 'info';
    return 'default';
  }

  formatEventType(eventType: string): string {
    return eventType.replace(/([A-Z])/g, ' $1').trim();
  }

  parseEventData(data?: string): Record<string, unknown> | null {
    if (!data) return null;
    try {
      return JSON.parse(data);
    } catch {
      return null;
    }
  }

  formatValue(value: unknown): string {
    if (value !== null && typeof value === 'object') return JSON.stringify(value);
    return String(value ?? '');
  }

  objectKeys(obj: Record<string, unknown>): string[] {
    return Object.keys(obj);
  }

}
