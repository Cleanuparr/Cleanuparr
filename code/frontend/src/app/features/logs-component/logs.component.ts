import { Component, ChangeDetectionStrategy, inject, computed, signal, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { NgIcon } from '@ng-icons/core';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import { CardComponent, BadgeComponent, ButtonComponent, SelectComponent, InputComponent, EmptyStateComponent, type SelectOption } from '@ui';
import { AppHubService } from '@core/realtime/app-hub.service';
import { ToastService } from '@core/services/toast.service';
import { AnimatedCounterComponent } from '@ui/animated-counter/animated-counter.component';
import { LogEntry } from '@core/models/signalr.models';

const LOG_LEVELS: SelectOption[] = [
  { label: 'All Levels', value: '' },
  { label: 'Error', value: 'error' },
  { label: 'Warning', value: 'warning' },
  { label: 'Information', value: 'information' },
  { label: 'Debug', value: 'debug' },
  { label: 'Trace', value: 'trace' },
];

@Component({
  selector: 'app-logs',
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
    EmptyStateComponent,
    AnimatedCounterComponent
  ],
  templateUrl: './logs.component.html',
  styleUrl: './logs.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LogsComponent implements OnInit {
  private readonly hub = inject(AppHubService);
  private readonly toast = inject(ToastService);
  private readonly route = inject(ActivatedRoute);

  readonly connected = this.hub.isConnected;
  readonly levelOptions = LOG_LEVELS;

  readonly selectedLevel = signal<unknown>('');
  readonly selectedCategory = signal<unknown>('');
  readonly searchQuery = signal('');
  readonly expandedIndex = signal<number | null>(null);
  readonly showExportMenu = signal(false);
  readonly selectedJobRunId = signal<string | null>(null);

  ngOnInit(): void {
    const runId = this.route.snapshot.queryParamMap.get('jobRunId');
    if (runId) {
      this.selectedJobRunId.set(runId);
    }
  }

  readonly filteredLogs = computed(() => {
    let logs = this.hub.logs();
    const level = this.selectedLevel() as string;
    const category = this.selectedCategory() as string;
    const query = this.searchQuery().toLowerCase();
    const runId = this.selectedJobRunId();

    if (runId) {
      logs = logs.filter((l) => l.jobRunId === runId);
    }
    if (level) {
      logs = logs.filter((l) => l.level.toLowerCase() === level);
    }
    if (category) {
      logs = logs.filter((l) => l.category === category);
    }
    if (query) {
      logs = logs.filter(
        (l) =>
          l.message.toLowerCase().includes(query) ||
          l.category?.toLowerCase().includes(query) ||
          l.exception?.toLowerCase().includes(query) ||
          l.instanceName?.toLowerCase().includes(query) ||
          l.downloadClientType?.toLowerCase().includes(query) ||
          l.downloadClientName?.toLowerCase().includes(query) ||
          l.jobRunId?.toLowerCase().includes(query)
      );
    }
    return logs.slice().sort((a, b) =>
      new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime()
    );
  });

  readonly categoryOptions = computed<SelectOption[]>(() => {
    const cats = new Set(this.hub.logs().map((l) => l.category).filter(Boolean));
    return [
      { label: 'All Categories', value: '' },
      ...Array.from(cats).sort().map((c) => ({ label: c!, value: c! })),
    ];
  });

  isExpandable(log: LogEntry): boolean {
    return !!(log.exception || log.jobName || log.instanceName || log.downloadClientType || log.jobRunId || log.message.length > 80);
  }

  toggleExpand(index: number): void {
    this.expandedIndex.update((current) => (current === index ? null : index));
  }

  clearLogs(): void {
    this.hub.clearLogs();
    this.toast.info('Logs cleared');
  }

  refreshLogs(): void {
    this.hub.requestRecentLogs();
    this.toast.info('Refreshing logs...');
  }

  copyLog(log: LogEntry): void {
    const text = `[${log.timestamp}] [${log.level}] ${log.category ? `[${log.category}] ` : ''}${log.message}${log.exception ? '\n' + log.exception : ''}`;
    navigator.clipboard.writeText(text);
    this.toast.success('Log copied');
  }

  copyAllLogs(): void {
    const text = this.filteredLogs()
      .map((l) => `[${l.timestamp}] [${l.level}] ${l.category ? `[${l.category}] ` : ''}${l.message}`)
      .join('\n');
    navigator.clipboard.writeText(text);
    this.toast.success(`${this.filteredLogs().length} logs copied`);
  }

  exportLogs(format: 'json' | 'csv' | 'text'): void {
    this.showExportMenu.set(false);
    const logs = this.filteredLogs();
    let content: string;
    let mimeType: string;
    let ext: string;

    switch (format) {
      case 'json':
        content = JSON.stringify(logs, null, 2);
        mimeType = 'application/json';
        ext = 'json';
        break;
      case 'csv': {
        const header = 'Timestamp,Level,Category,Message,Exception,JobName,InstanceName,DownloadClientType,DownloadClientName,JobRunId';
        const rows = logs.map((l) =>
          [l.timestamp, l.level, l.category ?? '', `"${(l.message ?? '').replace(/"/g, '""')}"`, `"${(l.exception ?? '').replace(/"/g, '""')}"`, l.jobName ?? '', l.instanceName ?? '', l.downloadClientType ?? '', l.downloadClientName ?? '', l.jobRunId ?? ''].join(',')
        );
        content = [header, ...rows].join('\n');
        mimeType = 'text/csv';
        ext = 'csv';
        break;
      }
      case 'text':
        content = logs
          .map((l) => `[${l.timestamp}] [${l.level}] ${l.category ? `[${l.category}] ` : ''}${l.message}${l.exception ? '\n' + l.exception : ''}`)
          .join('\n');
        mimeType = 'text/plain';
        ext = 'txt';
        break;
    }

    const blob = new Blob([content], { type: mimeType });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `cleanuparr-logs.${ext}`;
    a.click();
    URL.revokeObjectURL(url);
    this.toast.success(`Logs exported as ${format.toUpperCase()}`);
  }

  filterByJobRunId(runId: string): void {
    this.selectedJobRunId.set(runId);
  }

  clearJobRunFilter(): void {
    this.selectedJobRunId.set(null);
  }

  jobDisplayName(jobType: string): string {
    switch (jobType) {
      case 'QueueCleaner': return 'Queue Cleaner';
      case 'MalwareBlocker': return 'Malware Blocker';
      case 'DownloadCleaner': return 'Download Cleaner';
      case 'BlacklistSynchronizer': return 'Blacklist Sync';
      default: return jobType;
    }
  }

  logSeverity(level: string): 'error' | 'warning' | 'info' | 'success' | 'default' {
    const l = level.toLowerCase();
    if (l === 'error' || l === 'fatal' || l === 'critical') return 'error';
    if (l === 'warning') return 'warning';
    if (l === 'information' || l === 'info') return 'info';
    if (l === 'debug' || l === 'trace' || l === 'verbose') return 'success';
    return 'default';
  }

  logLevelLabel(level: string): string {
    const l = level.toLowerCase();
    if (l === 'information') return 'Info';
    return level.charAt(0).toUpperCase() + level.slice(1).toLowerCase();
  }
}
