import { Injectable, OnDestroy, inject, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { firstValueFrom } from 'rxjs';
import { LogEntry } from '@core/models/signalr.models';
import { AppEvent, ManualEvent } from '@core/models/event.models';
import { JobInfo } from '@core/models/job.models';
import { AppStatus } from '@core/models/app-status.model';
import { RecentStrike } from '@core/models/strike.models';
import { ApplicationPathService } from '@core/services/base-path.service';
import { AuthService } from '@core/auth/auth.service';

const MAX_BUFFER = 1000;
const HUB_URL = '/api/hubs/app';
const RECONNECT_DELAY_MS = 2000;

@Injectable({ providedIn: 'root' })
export class AppHubService implements OnDestroy {
  private readonly pathService = inject(ApplicationPathService);
  private readonly authService = inject(AuthService);
  private connection: signalR.HubConnection | null = null;
  private reconnectTimeout: ReturnType<typeof setTimeout> | null = null;

  private readonly connected = signal(false);
  readonly isConnected = this.connected.asReadonly();

  private readonly _logs = signal<LogEntry[]>([]);
  private readonly _events = signal<AppEvent[]>([]);
  private readonly _manualEvents = signal<ManualEvent[]>([]);
  private readonly _strikes = signal<RecentStrike[]>([]);
  private readonly _jobs = signal<JobInfo[]>([]);
  private readonly _appStatus = signal<AppStatus | null>(null);
  private readonly _cfScoresVersion = signal(0);
  private readonly _searchStatsVersion = signal(0);

  readonly logs = this._logs.asReadonly();
  readonly events = this._events.asReadonly();
  readonly manualEvents = this._manualEvents.asReadonly();
  readonly strikes = this._strikes.asReadonly();
  readonly jobs = this._jobs.asReadonly();
  readonly appStatus = this._appStatus.asReadonly();
  readonly cfScoresVersion = this._cfScoresVersion.asReadonly();
  readonly searchStatsVersion = this._searchStatsVersion.asReadonly();

  async start(): Promise<void> {
    if (this.connection) return;

    const hubUrl = this.pathService.buildHubUrl(HUB_URL);

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: async () => {
          // No tokens stored — trusted network bypass, no token needed
          if (!this.authService.getAccessToken() && !localStorage.getItem('refresh_token')) {
            return '';
          }
          if (this.authService.isTokenExpired(30)) {
            const result = await firstValueFrom(this.authService.refreshToken());
            if (result) {
              return result.accessToken;
            }
            return '';
          }
          return this.authService.getAccessToken() ?? '';
        },
      })
      .withAutomaticReconnect({
        // infinite retries with capped exponential backoff
        nextRetryDelayInMilliseconds: (retryContext) =>
          Math.min(RECONNECT_DELAY_MS * Math.pow(2, retryContext.previousRetryCount), 30_000),
      })
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.connection.onreconnecting(() => this.connected.set(false));
    this.connection.onreconnected(() => {
      this.connected.set(true);
      this.requestInitialData();
    });
    this.connection.onclose(() => this.connected.set(false));

    this.registerHandlers(this.connection);

    try {
      await this.connection.start();
      this.connected.set(true);
      this.requestInitialData();
    } catch (err) {
      // withAutomaticReconnect does not retry a failed initial connection, so retry it here
      console.warn('[SignalR] Connection failed:', err);
      this.connection = null;
      this.reconnectTimeout = setTimeout(() => this.start(), RECONNECT_DELAY_MS);
    }
  }

  async stop(): Promise<void> {
    if (this.reconnectTimeout) {
      clearTimeout(this.reconnectTimeout);
      this.reconnectTimeout = null;
    }
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
    }
    this.connected.set(false);
  }

  ngOnDestroy(): void {
    this.stop();
  }

  private invoke(method: string, ...args: unknown[]): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      return Promise.resolve();
    }
    return this.connection.invoke(method, ...args);
  }

  private registerHandlers(connection: signalR.HubConnection): void {
    // Single log entry
    connection.on('LogReceived', (log: LogEntry) => {
      this._logs.update((logs) => {
        const updated = [log, ...logs];
        return updated.length > MAX_BUFFER ? updated.slice(0, MAX_BUFFER) : updated;
      });
    });

    // Bulk initial logs
    connection.on('LogsReceived', (logs: LogEntry[]) => {
      this._logs.set([...logs].reverse());
    });

    // Single event (deduplicate by ID to handle updates like search completion)
    connection.on('EventReceived', (event: AppEvent) => {
      this._events.update((events) => {
        const filtered = events.filter((e) => e.id !== event.id);
        const updated = [event, ...filtered];
        return updated.length > MAX_BUFFER ? updated.slice(0, MAX_BUFFER) : updated;
      });
    });

    // Bulk initial events
    connection.on('EventsReceived', (events: AppEvent[]) => {
      this._events.set(events);
    });

    // Single manual event (deduplicate by ID)
    connection.on('ManualEventReceived', (event: ManualEvent) => {
      this._manualEvents.update((events) => {
        const filtered = events.filter((e) => e.id !== event.id);
        const updated = [event, ...filtered];
        return updated.length > MAX_BUFFER ? updated.slice(0, MAX_BUFFER) : updated;
      });
    });

    // Bulk initial manual events
    connection.on('ManualEventsReceived', (events: ManualEvent[]) => {
      this._manualEvents.set(events);
    });

    // Single strike (deduplicate by ID)
    connection.on('StrikeReceived', (strike: RecentStrike) => {
      this._strikes.update((strikes) => {
        const filtered = strikes.filter((s) => s.id !== strike.id);
        const updated = [strike, ...filtered];
        return updated.length > MAX_BUFFER ? updated.slice(0, MAX_BUFFER) : updated;
      });
    });

    // Bulk initial strikes
    connection.on('StrikesReceived', (strikes: RecentStrike[]) => {
      this._strikes.set(strikes);
    });

    // Jobs status
    connection.on('JobsStatusUpdate', (jobs: JobInfo[]) => {
      this._jobs.set(jobs);
    });

    connection.on('JobStatusUpdate', (job: JobInfo) => {
      this._jobs.update((jobs) => {
        const idx = jobs.findIndex((j) => j.jobType === job.jobType);
        if (idx >= 0) {
          const copy = [...jobs];
          copy[idx] = job;
          return copy;
        }
        return [...jobs, job];
      });
    });

    // App status
    connection.on('AppStatusUpdated', (status: AppStatus) => {
      this._appStatus.set(status);
    });

    // CF scores refresh
    connection.on('CfScoresUpdated', () => {
      this._cfScoresVersion.update(v => v + 1);
    });

    // Search stats refresh
    connection.on('SearchStatsUpdated', () => {
      this._searchStatsVersion.update(v => v + 1);
    });
  }

  private requestInitialData(): void {
    this.requestRecentLogs();
    this.requestRecentEvents();
    this.requestRecentStrikes();
    this.requestJobStatus();
  }

  requestRecentLogs(): void {
    this.invoke('GetRecentLogs');
  }

  requestRecentEvents(count = 10): void {
    this.invoke('GetRecentEvents', count);
  }

  requestRecentManualEvents(count = 100): void {
    this.invoke('GetRecentManualEvents', count);
  }

  requestRecentStrikes(count = 5): void {
    this.invoke('GetRecentStrikes', count);
  }

  requestJobStatus(): void {
    this.invoke('GetJobStatus');
  }

  clearLogs(): void {
    this._logs.set([]);
  }

  clearEvents(): void {
    this._events.set([]);
  }

  clearManualEvents(): void {
    this._manualEvents.set([]);
  }

  removeManualEvent(eventId: string): void {
    this._manualEvents.update((events) => events.filter((e) => e.id !== eventId));
  }
}
