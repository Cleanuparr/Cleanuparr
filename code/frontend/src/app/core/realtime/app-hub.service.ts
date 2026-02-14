import { Injectable, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { HubService } from './hub.service';
import { SignalRHubConfig, LogEntry } from '@core/models/signalr.models';
import { AppEvent, ManualEvent } from '@core/models/event.models';
import { JobInfo } from '@core/models/job.models';
import { AppStatus } from '@core/models/app-status.model';
import { RecentStrike } from '@core/models/strike.models';

const MAX_BUFFER = 1000;

@Injectable({ providedIn: 'root' })
export class AppHubService extends HubService {
  protected readonly config: SignalRHubConfig = {
    hubUrl: '/api/hubs/app',
    maxReconnectAttempts: 0, // infinite
    reconnectDelayMs: 2000,
    bufferSize: MAX_BUFFER,
    healthCheckIntervalMs: 0,
  };

  // Signal-based state
  private readonly _logs = signal<LogEntry[]>([]);
  private readonly _events = signal<AppEvent[]>([]);
  private readonly _manualEvents = signal<ManualEvent[]>([]);
  private readonly _strikes = signal<RecentStrike[]>([]);
  private readonly _jobs = signal<JobInfo[]>([]);
  private readonly _appStatus = signal<AppStatus | null>(null);

  readonly logs = this._logs.asReadonly();
  readonly events = this._events.asReadonly();
  readonly manualEvents = this._manualEvents.asReadonly();
  readonly strikes = this._strikes.asReadonly();
  readonly jobs = this._jobs.asReadonly();
  readonly appStatus = this._appStatus.asReadonly();

  protected registerHandlers(connection: signalR.HubConnection): void {
    // Single log entry
    connection.on('LogReceived', (log: LogEntry) => {
      this._logs.update((logs) => {
        const updated = [log, ...logs];
        return updated.length > MAX_BUFFER ? updated.slice(0, MAX_BUFFER) : updated;
      });
    });

    // Bulk initial logs
    connection.on('LogsReceived', (logs: LogEntry[]) => {
      this._logs.set(logs);
    });

    // Single event
    connection.on('EventReceived', (event: AppEvent) => {
      this._events.update((events) => {
        const updated = [event, ...events];
        return updated.length > MAX_BUFFER ? updated.slice(0, MAX_BUFFER) : updated;
      });
    });

    // Bulk initial events
    connection.on('EventsReceived', (events: AppEvent[]) => {
      this._events.set(events);
    });

    // Single manual event
    connection.on('ManualEventReceived', (event: ManualEvent) => {
      this._manualEvents.update((events) => {
        const updated = [event, ...events];
        return updated.length > MAX_BUFFER ? updated.slice(0, MAX_BUFFER) : updated;
      });
    });

    // Bulk initial manual events
    connection.on('ManualEventsReceived', (events: ManualEvent[]) => {
      this._manualEvents.set(events);
    });

    // Single strike
    connection.on('StrikeReceived', (strike: RecentStrike) => {
      this._strikes.update((strikes) => {
        const updated = [strike, ...strikes];
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
  }

  protected override onConnected(): void {
    this.requestRecentLogs();
    this.requestRecentEvents();
    this.requestRecentManualEvents();
    this.requestRecentStrikes();
    this.requestJobStatus();
  }

  protected override onReconnected(): void {
    this.onConnected();
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
