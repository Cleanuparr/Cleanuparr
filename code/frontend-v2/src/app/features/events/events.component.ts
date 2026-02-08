import { Component, ChangeDetectionStrategy, inject, signal, OnInit, OnDestroy } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NgIcon } from '@ng-icons/core';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import {
  CardComponent, BadgeComponent, ButtonComponent, SelectComponent,
  InputComponent, PaginatorComponent, EmptyStateComponent, type SelectOption,
} from '@ui';
import { EventsApi } from '@core/api/events.api';
import { ToastService } from '@core/services/toast.service';
import { AppEvent, EventFilter } from '@core/models/event.models';

@Component({
  selector: 'app-events',
  standalone: true,
  imports: [
    DatePipe,
    FormsModule,
    NgIcon,
    PageHeaderComponent,
    CardComponent,
    BadgeComponent,
    ButtonComponent,
    SelectComponent,
    InputComponent,
    PaginatorComponent,
    EmptyStateComponent,
  ],
  templateUrl: './events.component.html',
  styleUrl: './events.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EventsComponent implements OnInit, OnDestroy {
  private readonly eventsApi = inject(EventsApi);
  private readonly toast = inject(ToastService);
  private pollTimer: ReturnType<typeof setInterval> | null = null;

  readonly events = signal<AppEvent[]>([]);
  readonly totalRecords = signal(0);
  readonly loading = signal(false);
  readonly expandedId = signal<string | null>(null);

  readonly currentPage = signal(1);
  readonly pageSize = signal(50);
  readonly selectedSeverity = signal<unknown>('');
  readonly selectedType = signal<unknown>('');
  readonly searchQuery = signal('');

  readonly severityOptions = signal<SelectOption[]>([{ label: 'All Severities', value: '' }]);
  readonly typeOptions = signal<SelectOption[]>([{ label: 'All Types', value: '' }]);

  ngOnInit(): void {
    this.loadFilterOptions();
    this.loadEvents();
    this.pollTimer = setInterval(() => this.loadEvents(), 10_000);
  }

  ngOnDestroy(): void {
    if (this.pollTimer) {
      clearInterval(this.pollTimer);
    }
  }

  loadEvents(): void {
    const filter: EventFilter = {
      page: this.currentPage(),
      pageSize: this.pageSize(),
    };
    const severity = this.selectedSeverity() as string;
    const type = this.selectedType() as string;
    const search = this.searchQuery();

    if (severity) filter.severity = severity;
    if (type) filter.eventType = type;
    if (search) filter.search = search;

    this.loading.set(true);
    this.eventsApi.getEvents(filter).subscribe({
      next: (result) => {
        this.events.set(result.items);
        this.totalRecords.set(result.totalCount);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.error('Failed to load events');
      },
    });
  }

  private loadFilterOptions(): void {
    this.eventsApi.getSeverities().subscribe({
      next: (severities) => {
        this.severityOptions.set([
          { label: 'All Severities', value: '' },
          ...severities.map((s) => ({ label: s, value: s })),
        ]);
      },
    });
    this.eventsApi.getEventTypes().subscribe({
      next: (types) => {
        this.typeOptions.set([
          { label: 'All Types', value: '' },
          ...types.map((t) => ({ label: this.formatEventType(t), value: t })),
        ]);
      },
    });
  }

  onFilterChange(): void {
    this.currentPage.set(1);
    this.loadEvents();
  }

  onPageChange(page: number): void {
    this.currentPage.set(page);
    this.loadEvents();
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
    this.loadEvents();
  }

  // Helpers
  eventSeverity(severity: string): 'error' | 'warning' | 'info' | 'primary' | 'default' {
    const s = severity.toLowerCase();
    if (s === 'error') return 'error';
    if (s === 'warning') return 'warning';
    if (s === 'information' || s === 'info') return 'info';
    if (s === 'important') return 'primary';
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

  objectKeys(obj: Record<string, unknown>): string[] {
    return Object.keys(obj);
  }

  truncate(text: string, max = 100): string {
    return text.length > max ? text.substring(0, max) + '...' : text;
  }
}
