import { Component, ChangeDetectionStrategy, inject, computed, signal, OnInit } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { DatePipe, JsonPipe } from '@angular/common';
import { NgIcon } from '@ng-icons/core';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import { CardComponent, ButtonComponent, BadgeComponent, SpinnerComponent } from '@ui';
import { AppHubService } from '@core/realtime/app-hub.service';
import { EventsApi } from '@core/api/events.api';
import { JobsApi } from '@core/api/jobs.api';
import { GeneralConfigApi } from '@core/api/general-config.api';
import { ToastService } from '@core/services/toast.service';
import { LogEntry } from '@core/models/signalr.models';
import { ManualEvent } from '@core/models/event.models';
import { JobType } from '@shared/models/enums';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    RouterLink,
    DatePipe,
    JsonPipe,
    NgIcon,
    PageHeaderComponent,
    CardComponent,
    ButtonComponent,
    BadgeComponent,
    SpinnerComponent,
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardComponent implements OnInit {
  private readonly hub = inject(AppHubService);
  private readonly eventsApi = inject(EventsApi);
  private readonly jobsApi = inject(JobsApi);
  private readonly generalConfigApi = inject(GeneralConfigApi);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);

  readonly connected = this.hub.isConnected;
  readonly jobs = this.hub.jobs;
  readonly showSupportSection = signal(false);

  readonly recentStrikes = computed(() => this.hub.strikes().slice(0, 5));
  readonly recentLogs = computed(() => this.hub.logs().slice(0, 5));
  readonly recentEvents = computed(() => this.hub.events().slice(0, 5));

  readonly unresolvedManualEvents = computed(() =>
    this.hub.manualEvents().filter((e) => !e.isResolved)
  );

  readonly manualEventIndex = signal(0);

  readonly currentManualEvent = computed(() => {
    const events = this.unresolvedManualEvents();
    const idx = this.manualEventIndex();
    return events[idx] ?? null;
  });

  readonly canNavigatePrev = computed(() => this.manualEventIndex() > 0);
  readonly canNavigateNext = computed(() =>
    this.manualEventIndex() < this.unresolvedManualEvents().length - 1
  );

  ngOnInit(): void {
    this.generalConfigApi.get().subscribe({
      next: (config) => this.showSupportSection.set(config.displaySupportBanner),
    });
  }

  // Manual event navigation
  prevManualEvent(): void {
    if (this.canNavigatePrev()) {
      this.manualEventIndex.update((i) => i - 1);
    }
  }

  nextManualEvent(): void {
    if (this.canNavigateNext()) {
      this.manualEventIndex.update((i) => i + 1);
    }
  }

  dismissManualEvent(event: ManualEvent): void {
    this.eventsApi.resolveManualEvent(event.id).subscribe({
      next: () => {
        this.hub.removeManualEvent(event.id);
        const maxIdx = this.unresolvedManualEvents().length - 1;
        if (this.manualEventIndex() > maxIdx) {
          this.manualEventIndex.set(Math.max(0, maxIdx));
        }
        this.toast.success('Event dismissed');
      },
      error: () => this.toast.error('Failed to dismiss event'),
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

  getDownloadName(event: { data?: string }): string | null {
    if (!event.data) return null;
    try {
      return JSON.parse(event.data)?.itemName || null;
    } catch {
      return null;
    }
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

  parseEventData(data: string | undefined): unknown {
    if (!data) return null;
    try {
      return JSON.parse(data);
    } catch {
      return null;
    }
  }

  navigateTo(path: string): void {
    this.router.navigate([path]);
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
}
