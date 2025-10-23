import { inject, Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import * as signalR from '@microsoft/signalr';
import { LogEntry } from '../models/signalr.models';
import { AppEvent, ManualEvent } from '../models/event.models';
import { AppStatus } from '../models/app-status.model';
import { JobInfo } from '../models/job.models';
import { ApplicationPathService } from './base-path.service';

/**
 * Unified SignalR hub service
 */
@Injectable({
  providedIn: 'root'
})
export class AppHubService {
  private hubConnection!: signalR.HubConnection;
  private connectionStatusSubject = new BehaviorSubject<boolean>(false);
  private logsSubject = new BehaviorSubject<LogEntry[]>([]);
  private eventsSubject = new BehaviorSubject<AppEvent[]>([]);
  private manualEventsSubject = new BehaviorSubject<ManualEvent[]>([]);
  private appStatusSubject = new BehaviorSubject<AppStatus | null>(null);
  private jobsSubject = new BehaviorSubject<JobInfo[]>([]);
  private readonly ApplicationPathService = inject(ApplicationPathService);

  private logBuffer: LogEntry[] = [];
  private eventBuffer: AppEvent[] = [];
  private manualEventBuffer: ManualEvent[] = [];
  private readonly bufferSize = 1000;

  constructor() { }
  
  /**
   * Start the SignalR connection
   */
  public startConnection(): Promise<void> {
    if (this.hubConnection && 
        this.hubConnection.state !== signalR.HubConnectionState.Disconnected) {
      return Promise.resolve();
    }

    // Build a new connection
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(this.ApplicationPathService.buildApiUrl('/hubs/app'))
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          // Implement exponential backoff with max 30 seconds
          return Math.min(2000 * Math.pow(2, retryContext.previousRetryCount), 30000);
        }
      })
      .build();

    this.registerSignalREvents();

    return this.hubConnection.start()
      .then(() => {
        this.connectionStatusSubject.next(true);
        this.requestInitialData();
      })
      .catch(err => {
        this.connectionStatusSubject.next(false);
        throw err;
      });
  }
  
  /**
   * Register SignalR event handlers
   */
  private registerSignalREvents(): void {
    // Handle connection events
    this.hubConnection.onreconnected(() => {
      this.connectionStatusSubject.next(true);
      this.requestInitialData();
    });

    this.hubConnection.onreconnecting(() => {
      this.connectionStatusSubject.next(false);
      this.appStatusSubject.next(null);
    });

    this.hubConnection.onclose(() => {
      this.connectionStatusSubject.next(false);
      this.appStatusSubject.next(null);
    });

    // Handle individual log messages
    this.hubConnection.on('LogReceived', (log: LogEntry) => {
      this.addLogToBuffer(log);
      const currentLogs = this.logsSubject.value;
      this.logsSubject.next([...currentLogs, log]);
    });
    
    // Handle bulk log messages (initial load)
    this.hubConnection.on('LogsReceived', (logs: LogEntry[]) => {
      if (logs && logs.length > 0) {
        // Set all logs at once
        this.logsSubject.next(logs);
        // Update buffer
        this.logBuffer = [...logs];
        this.trimBuffer(this.logBuffer, this.bufferSize);
      }
    });
    
    // Handle individual event messages
    this.hubConnection.on('EventReceived', (event: AppEvent) => {
      this.addEventToBuffer(event);
      const currentEvents = this.eventsSubject.value;
      this.eventsSubject.next([...currentEvents, event]);
    });
    
    // Handle bulk event messages (initial load)
    this.hubConnection.on('EventsReceived', (events: AppEvent[]) => {
      if (events && events.length > 0) {
        // Set all events at once
        this.eventsSubject.next(events);
        // Update buffer
        this.eventBuffer = [...events];
        this.trimBuffer(this.eventBuffer, this.bufferSize);
      }
    });

    // Handle individual manual event messages
    this.hubConnection.on('ManualEventReceived', (event: ManualEvent) => {
      this.addManualEventToBuffer(event);
      const currentEvents = this.manualEventsSubject.value;
      this.manualEventsSubject.next([...currentEvents, event]);
    });

    // Handle bulk manual event messages (initial load)
    this.hubConnection.on('ManualEventsReceived', (events: ManualEvent[]) => {
      if (events && events.length > 0) {
        // Set all manual events at once
        this.manualEventsSubject.next(events);
        // Update buffer
        this.manualEventBuffer = [...events];
        this.trimBuffer(this.manualEventBuffer, this.bufferSize);
      }
    });

    this.hubConnection.on('AppStatusUpdated', (status: AppStatus | null) => {
      if (!status) {
        this.appStatusSubject.next(null);
        return;
      }

      const normalized: AppStatus = {
        currentVersion: status.currentVersion ?? null,
        latestVersion: status.latestVersion ?? null
      };

      this.appStatusSubject.next(normalized);
    });

    // Handle job status updates
    this.hubConnection.on('JobsStatusUpdate', (jobs: JobInfo[]) => {
      if (jobs) {
        this.jobsSubject.next(jobs);
      }
    });

    this.hubConnection.on('JobStatusUpdate', (job: JobInfo) => {
      if (job) {
        const currentJobs = this.jobsSubject.value;
        const jobIndex = currentJobs.findIndex(j => j.name === job.name);
        if (jobIndex !== -1) {
          currentJobs[jobIndex] = job;
          this.jobsSubject.next([...currentJobs]);
        } else {
          this.jobsSubject.next([...currentJobs, job]);
        }
      }
    });
  }
  
  /**
   * Request initial data from the server
   */
  private requestInitialData(): void {
    this.requestRecentLogs();
    this.requestRecentEvents();
    this.requestRecentManualEvents();
    this.requestJobStatus();
  }
  
  /**
   * Request recent logs from the server
   */
  public requestRecentLogs(): void {
    if (this.isConnected()) {
      this.hubConnection.invoke('GetRecentLogs')
        .catch(err => console.error('Error requesting recent logs:', err));
    }
  }
  
  /**
   * Request recent events from the server
   */
  public requestRecentEvents(count: number = 100): void {
    if (this.isConnected()) {
      this.hubConnection.invoke('GetRecentEvents', count)
        .catch(err => console.error('Error requesting recent events:', err));
    }
  }

  /**
   * Request recent manual events from the server
   */
  public requestRecentManualEvents(count: number = 100): void {
    if (this.isConnected()) {
      this.hubConnection.invoke('GetRecentManualEvents', count)
        .catch(err => console.error('Error requesting recent manual events:', err));
    }
  }

  /**
   * Check if the connection is established
   */
  private isConnected(): boolean {
    return this.hubConnection &&
           this.hubConnection.state === signalR.HubConnectionState.Connected;
  }
  
  /**
   * Stop the SignalR connection
   */
  public stopConnection(): Promise<void> {
    if (!this.hubConnection) {
      return Promise.resolve();
    }
    
    return this.hubConnection.stop()
      .then(() => {
        this.connectionStatusSubject.next(false);
      })
      .catch(err => {
        console.error('Error stopping AppHub connection:', err);
        throw err;
      });
  }
  
  /**
   * Add a log to the buffer
   */
  private addLogToBuffer(log: LogEntry): void {
    this.logBuffer.push(log);
    this.trimBuffer(this.logBuffer, this.bufferSize);
  }
  
  /**
   * Add an event to the buffer
   */
  private addEventToBuffer(event: AppEvent): void {
    this.eventBuffer.push(event);
    this.trimBuffer(this.eventBuffer, this.bufferSize);
  }

  /**
   * Add a manual event to the buffer
   */
  private addManualEventToBuffer(event: ManualEvent): void {
    this.manualEventBuffer.push(event);
    this.trimBuffer(this.manualEventBuffer, this.bufferSize);
  }

  /**
   * Trim a buffer to the specified size
   */
  private trimBuffer<T>(buffer: T[], maxSize: number): void {
    while (buffer.length > maxSize) {
      buffer.shift();
    }
  }
  
  // PUBLIC API METHODS
  
  /**
   * Get logs as an observable
   */
  public getLogs(): Observable<LogEntry[]> {
    return this.logsSubject.asObservable();
  }
  
  /**
   * Get events as an observable
   */
  public getEvents(): Observable<AppEvent[]> {
    return this.eventsSubject.asObservable();
  }

  /**
   * Get manual events as an observable
   */
  public getManualEvents(): Observable<ManualEvent[]> {
    return this.manualEventsSubject.asObservable();
  }

  /**
   * Get jobs as an observable
   */
  public getJobs(): Observable<JobInfo[]> {
    return this.jobsSubject.asObservable();
  }
  
  /**
   * Get jobs connection status as an observable
   * For consistency with logs and events connection status
   */
  public getJobsConnectionStatus(): Observable<boolean> {
    return this.connectionStatusSubject.asObservable();
  }
  
  /**
   * Request job status from the server
   */
  public requestJobStatus(): void {
    if (this.isConnected()) {
      this.hubConnection.invoke('GetJobStatus')
        .catch(err => console.error('Error requesting job status:', err));
    }
  }
  
  /**
   * Get connection status as an observable
   */
  public getConnectionStatus(): Observable<boolean> {
    return this.connectionStatusSubject.asObservable();
  }
  
  /**
   * Get logs connection status as an observable
   * For backward compatibility with components expecting separate connection statuses
   */
  public getLogsConnectionStatus(): Observable<boolean> {
    return this.connectionStatusSubject.asObservable();
  }
  
  /**
   * Get events connection status as an observable
   * For backward compatibility with components expecting separate connection statuses
   */
  public getEventsConnectionStatus(): Observable<boolean> {
    return this.connectionStatusSubject.asObservable();
  }

  public getAppStatus(): Observable<AppStatus | null> {
    return this.appStatusSubject.asObservable();
  }
  
  /**
   * Clear events
   */
  public clearEvents(): void {
    this.eventsSubject.next([]);
    this.eventBuffer = [];
  }

  /**
   * Clear manual events
   */
  public clearManualEvents(): void {
    this.manualEventsSubject.next([]);
    this.manualEventBuffer = [];
  }

  /**
   * Clear logs
   */
  public clearLogs(): void {
    this.logsSubject.next([]);
    this.logBuffer = [];
  }

  /**
   * Remove a specific manual event from the subject
   */
  public removeManualEvent(eventId: string): void {
    const currentEvents = this.manualEventsSubject.value;
    const filteredEvents = currentEvents.filter(e => e.id !== eventId);
    this.manualEventsSubject.next(filteredEvents);
  }
}
