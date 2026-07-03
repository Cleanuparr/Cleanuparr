import { Component, ChangeDetectionStrategy, inject, computed, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import type { Observable } from 'rxjs';
import { Router, RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { NgIcon } from '@ng-icons/core';
import { CdkDragDrop, CdkDropList, CdkDrag, CdkDragHandle, moveItemInArray } from '@angular/cdk/drag-drop';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import { CardComponent, ButtonComponent, BadgeComponent, SpinnerComponent } from '@ui';
import { AppHubService } from '@core/realtime/app-hub.service';
import { EventsApi } from '@core/api/events.api';
import { JobsApi } from '@core/api/jobs.api';
import { GeneralConfigApi } from '@core/api/general-config.api';
import { CfScoreApi, CfScoreStats, CfScoreUpgradesResponse } from '@core/api/cf-score.api';
import { ToastService } from '@core/services/toast.service';
import { ConfirmService } from '@core/services/confirm.service';
import { ManualEvent } from '@core/models/event.models';
import { JobType } from '@shared/models/enums';
import { StatsCardComponent } from './stats-card/stats-card.component';

const DASHBOARD_ROW_ORDER_KEY = 'dashboard-row-order';
const DEFAULT_ROW_ORDER = ['strikes', 'logs-events', 'cf-scores', 'jobs'] as const;
type DashboardRowId = typeof DEFAULT_ROW_ORDER[number];

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    RouterLink,
    DatePipe,
    NgIcon,
    PageHeaderComponent,
    CardComponent,
    ButtonComponent,
    BadgeComponent,
    SpinnerComponent,
    CdkDropList,
    CdkDrag,
    CdkDragHandle,
    StatsCardComponent,
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardComponent {
  readonly JobType = JobType;

  private readonly hub = inject(AppHubService);
  private readonly eventsApi = inject(EventsApi);
  private readonly jobsApi = inject(JobsApi);
  private readonly generalConfigApi = inject(GeneralConfigApi);
  private readonly cfScoreApi = inject(CfScoreApi);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmService);
  private readonly router = inject(Router);

  private static readonly MANUAL_PAGE_SIZE = 20;

  constructor() {
    this.loadMoreManualEvents();
  }

  readonly connected = this.hub.isConnected;
  readonly jobs = this.hub.jobs;

  private readonly generalConfigResource = rxResource({
    stream: () => this.generalConfigApi.get(),
  });
  private readonly cfScoreStatsResource = rxResource({
    stream: (): Observable<CfScoreStats | null> => this.cfScoreApi.getStats(),
    defaultValue: null,
  });
  private readonly cfScoreUpgradesResource = rxResource({
    stream: () => this.cfScoreApi.getRecentUpgrades({ page: 1, pageSize: 5 }),
    defaultValue: { items: [], page: 1, pageSize: 5, totalCount: 0, totalPages: 0 } as CfScoreUpgradesResponse,
  });

  readonly showSupportSection = computed(() =>
    this.generalConfigResource.hasValue() ? this.generalConfigResource.value().displaySupportBanner : false,
  );
  readonly cfScoreStats = computed(() => this.cfScoreStatsResource.value());
  readonly cfScoreUpgrades = computed(() => this.cfScoreUpgradesResource.value().items);

  readonly rowOrder = signal<DashboardRowId[]>(this.loadOrder());
  readonly visibleRowOrder = computed(() => {
    const order = this.rowOrder();
    return this.cfScoreStats() ? order : order.filter((id) => id !== 'cf-scores');
  });

  readonly recentStrikes = computed(() => this.hub.strikes().slice(0, 5));
  readonly recentLogs = computed(() => this.hub.logs().slice(0, 5));
  readonly recentEvents = computed(() => this.hub.events().slice(0, 5));

  // REST-paged backlog of unresolved manual events (lazily loaded page by page)
  private readonly manualPages = signal<ManualEvent[]>([]);
  private readonly manualNextPage = signal(1);
  private readonly totalUnresolvedManualEvents = signal(0);
  readonly loadingMoreManualEvents = signal(false);

  // Merge live-pushed hub events with the lazily-paged backlog, newest first
  readonly unresolvedManualEvents = computed(() => {
    const byId = new Map<string, ManualEvent>();
    for (const e of this.hub.manualEvents()) {
      if (!e.isResolved) {
        byId.set(e.id, e);
      }
    }
    for (const e of this.manualPages()) {
      if (!e.isResolved) {
        byId.set(e.id, e);
      }
    }
    return [...byId.values()].sort((a, b) =>
      a.timestamp < b.timestamp ? 1 : a.timestamp > b.timestamp ? -1 : 0
    );
  });

  readonly manualEventIndex = signal(0);

  // True unresolved count; live pushes may exceed the last known server total
  readonly manualEventCount = computed(() =>
    Math.max(this.totalUnresolvedManualEvents(), this.unresolvedManualEvents().length)
  );

  readonly currentManualEvent = computed(() => {
    const events = this.unresolvedManualEvents();
    const idx = this.manualEventIndex();
    return events[idx] ?? null;
  });

  readonly canNavigatePrev = computed(() => this.manualEventIndex() > 0);
  readonly canNavigateNext = computed(() =>
    this.manualEventIndex() < this.manualEventCount() - 1
  );

  // Manual event navigation
  prevManualEvent(): void {
    if (this.canNavigatePrev()) {
      this.manualEventIndex.update((i) => i - 1);
    }
  }

  nextManualEvent(): void {
    if (!this.canNavigateNext()) {
      return;
    }
    const nextIdx = this.manualEventIndex() + 1;
    if (nextIdx >= this.unresolvedManualEvents().length) {
      // Reached the end of what's loaded — lazily fetch the next page first
      this.loadMoreManualEvents(() => {
        const maxIdx = this.unresolvedManualEvents().length - 1;
        this.manualEventIndex.set(Math.min(nextIdx, maxIdx));
      });
      return;
    }
    this.manualEventIndex.set(nextIdx);
  }

  private loadMoreManualEvents(after?: () => void): void {
    if (this.loadingMoreManualEvents()) {
      return;
    }
    this.loadingMoreManualEvents.set(true);
    const page = this.manualNextPage();
    this.eventsApi
      .getManualEvents({ page, pageSize: DashboardComponent.MANUAL_PAGE_SIZE, isResolved: false })
      .subscribe({
        next: (res) => {
          this.manualPages.update((cur) => [...cur, ...res.items]);
          this.manualNextPage.set(page + 1);
          this.totalUnresolvedManualEvents.set(res.totalCount);
          this.loadingMoreManualEvents.set(false);
          after?.();
        },
        error: () => {
          this.loadingMoreManualEvents.set(false);
          this.toast.error('Failed to load more events');
        },
      });
  }

  dismissManualEvent(event: ManualEvent): void {
    this.eventsApi.resolveManualEvent(event.id).subscribe({
      next: () => {
        this.hub.removeManualEvent(event.id);
        this.manualPages.update((cur) => cur.filter((e) => e.id !== event.id));
        this.totalUnresolvedManualEvents.update((t) => Math.max(0, t - 1));
        const maxIdx = this.unresolvedManualEvents().length - 1;
        if (this.manualEventIndex() > maxIdx) {
          this.manualEventIndex.set(Math.max(0, maxIdx));
        }
        this.toast.success('Event dismissed');
      },
      error: () => this.toast.error('Failed to dismiss event'),
    });
  }

  async dismissAllManualEvents(): Promise<void> {
    const confirmed = await this.confirm.confirm({
      title: 'Dismiss all events',
      message: `Dismiss all ${this.manualEventCount()} action required events? This marks them all as resolved.`,
      confirmLabel: 'Dismiss all',
    });
    if (!confirmed) {
      return;
    }
    this.eventsApi.resolveAllManualEvents().subscribe({
      next: (res) => {
        this.hub.clearManualEvents();
        this.manualPages.set([]);
        this.manualNextPage.set(1);
        this.totalUnresolvedManualEvents.set(0);
        this.manualEventIndex.set(0);
        this.toast.success(`Dismissed ${res.resolvedCount} events`);
      },
      error: () => this.toast.error('Failed to dismiss events'),
    });
  }

  triggerJob(jobType: string): void {
    this.jobsApi.trigger(jobType as JobType).subscribe({
      next: () => this.toast.success(`${this.jobDisplayName(jobType)} triggered`),
      error: () => this.toast.error(`Failed to trigger ${this.jobDisplayName(jobType)}`),
    });
  }

  // Log helpers
  logSeverity(level: string): 'error' | 'warning' | 'info' | 'success' | 'default' {
    const l = level.toLowerCase();
    if (l === 'error' || l === 'fatal' || l === 'critical') return 'error';
    if (l === 'warning') return 'warning';
    if (l === 'information' || l === 'info') return 'info';
    if (l === 'debug' || l === 'trace' || l === 'verbose') return 'success';
    return 'default';
  }

  logBadgeSeverity(level: string): 'error' | 'warning' | 'info' | 'success' | 'default' {
    return this.logSeverity(level);
  }

  logLevelLabel(level: string): string {
    const l = level.toLowerCase();
    if (l === 'information') return 'Info';
    return level.charAt(0).toUpperCase() + level.slice(1).toLowerCase();
  }

  // Event helpers
  eventMarkerClass(eventType: string, severity: string): string {
    const t = eventType.toLowerCase();
    if (t === 'strikereset') {
      return 'success';
    }
    if (t.includes('strike')) {
      const s = severity.toLowerCase();
      if (s === 'error') return 'error';
      if (s === 'warning') return 'warning';
      return 'warning'; // strikes default to yellow/amber
    }
    return this.eventSeverity(severity);
  }

  eventTypeSeverity(eventType: string): 'error' | 'warning' | 'info' | 'success' | 'default' {
    const t = eventType.toLowerCase();
    if (t === 'strikereset') return 'success';
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

  getDownloadName(event: { itemTitle?: string }): string | null {
    return event.itemTitle || null;
  }

  truncate(text: string, max = 80): string {
    return text.length > max ? text.substring(0, max) + '...' : text;
  }

  // Timeline icon helpers
  logIcon(level: string): string {
    const l = level.toLowerCase();
    if (l === 'error' || l === 'fatal' || l === 'critical') return 'tablerCircleX';
    if (l === 'warning') return 'tablerAlertTriangle';
    if (l === 'information' || l === 'info') return 'tablerInfoCircle';
    if (l === 'debug' || l === 'trace' || l === 'verbose') return 'tablerCode';
    return 'tablerCircle';
  }

  eventIcon(eventType: string): string {
    const t = eventType.toLowerCase();
    if (t === 'strikereset') return 'tablerHistory';
    if (t.includes('strike')) return 'tablerBolt';
    if (t === 'downloadcleaned') return 'tablerDownload';
    if (t === 'queueitemdeleted') return 'tablerTrash';
    if (t === 'categorychanged') return 'tablerTag';
    return 'tablerCircle';
  }

  // Job helpers
  jobDisplayName(jobType: string): string {
    switch (jobType) {
      case 'QueueCleaner': return 'Queue Cleaner';
      case 'MalwareBlocker': return 'Malware Blocker';
      case 'DownloadCleaner': return 'Download Cleaner';
      case 'BlacklistSynchronizer': return 'Blacklist Sync';
      default: return jobType;
    }
  }

  jobStatusSeverity(status: string): 'success' | 'warning' | 'error' | 'info' | 'default' {
    const s = status.toLowerCase();
    if (s === 'running') return 'info';
    if (s === 'complete' || s === 'scheduled') return 'success';
    if (s === 'error') return 'error';
    if (s === 'paused') return 'warning';
    return 'default';
  }

  manualEventSeverityClass(severity: string): string {
    const s = severity.toLowerCase();
    if (s === 'error') return 'manual-event--error';
    if (s === 'warning') return 'manual-event--warning';
    if (s === 'important') return 'manual-event--important';
    return 'manual-event--info';
  }

  processManualEventMessage(message: string): string {
    if (!message) return '';
    // Escape HTML to prevent XSS
    let processed = message
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
    // Convert newlines to <br> tags
    processed = processed.replace(/\\n/g, '<br>').replace(/\n/g, '<br>');
    // Convert URLs to clickable links
    const urlRegex = /(https?:\/\/[^\s<]+)/g;
    processed = processed.replace(urlRegex, '<a href="$1" target="_blank" rel="noopener noreferrer" class="manual-event-link">$1</a>');
    return processed;
  }

  manualEventDetails(event: ManualEvent): { label: string; value: string }[] {
    const details: { label: string; value: string }[] = [];
    if (event.itemTitle) {
      details.push({ label: 'Item', value: event.itemTitle });
    }
    if (event.itemHash) {
      details.push({ label: 'Hash', value: event.itemHash });
    }
    if (event.strikeCount !== null && event.strikeCount !== undefined) {
      details.push({ label: 'Strike count', value: String(event.strikeCount) });
    }
    return details;
  }

  navigateTo(path: string): void {
    this.router.navigate([path]);
  }

  private loadOrder(): DashboardRowId[] {
    try {
      const saved = localStorage.getItem(DASHBOARD_ROW_ORDER_KEY);
      if (saved) {
        const parsed: unknown[] = JSON.parse(saved);
        const valid = parsed.filter((id): id is DashboardRowId =>
          (DEFAULT_ROW_ORDER as readonly unknown[]).includes(id)
        );
        // Ensure any newly added rows (future-proofing) are appended
        for (const id of DEFAULT_ROW_ORDER) {
          if (!valid.includes(id)) valid.push(id);
        }
        return valid;
      }
    } catch { /* ignore */ }
    return [...DEFAULT_ROW_ORDER];
  }

  onDrop(event: CdkDragDrop<DashboardRowId[]>): void {
    const visible = [...this.visibleRowOrder()];
    moveItemInArray(visible, event.previousIndex, event.currentIndex);
    const hidden = this.rowOrder().filter((id) => !visible.includes(id));
    const newOrder = [...visible, ...hidden];
    this.rowOrder.set(newOrder);
    localStorage.setItem(DASHBOARD_ROW_ORDER_KEY, JSON.stringify(newOrder));
  }

  // Strike helpers
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

  instanceTypeSeverity(type: string): 'info' | 'warning' | 'default' {
    if (type === 'Radarr') return 'warning';
    if (type === 'Sonarr') return 'info';
    return 'default';
  }
}
