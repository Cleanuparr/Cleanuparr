import { Component, OnInit, OnDestroy, signal, computed, inject } from '@angular/core';
import { CommonModule, NgClass, DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';

// PrimeNG Components
import { CardModule } from 'primeng/card';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { ProgressSpinnerModule } from 'primeng/progressspinner';

// Services & Models
import { AppHubService } from '../../core/services/app-hub.service';
import { ConfigurationService } from '../../core/services/configuration.service';
import { LogEntry } from '../../core/models/signalr.models';
import { AppEvent } from '../../core/models/event.models';
import { GeneralConfig } from '../../shared/models/general-config.model';

// Components
import { SupportSectionComponent } from '../../shared/components/support-section/support-section.component';
import { JobsManagementComponent } from '../../shared/components/jobs-management/jobs-management.component';

@Component({
  selector: 'app-dashboard-page',
  standalone: true,
  imports: [
    CommonModule,
    NgClass,
    RouterLink,
    DatePipe,
    CardModule,
    ButtonModule,
    TagModule,
    TooltipModule,
    ProgressSpinnerModule,
    SupportSectionComponent,
    JobsManagementComponent
  ],
  templateUrl: './dashboard-page.component.html',
  styleUrl: './dashboard-page.component.scss'
})
export class DashboardPageComponent implements OnInit, OnDestroy {
  private appHubService = inject(AppHubService);
  private configurationService = inject(ConfigurationService);
  private destroy$ = new Subject<void>();

  // Signals for reactive state
  recentLogs = signal<LogEntry[]>([]);
  recentEvents = signal<AppEvent[]>([]);
  connected = signal<boolean>(false);
  generalConfig = signal<GeneralConfig | null>(null);

  // Computed values for display
  displayLogs = computed(() => {
    return this.recentLogs()
      .sort((a, b) => new Date(a.timestamp).getTime() - new Date(b.timestamp).getTime()) // Sort chronologically (oldest first)
      .slice(-5); // Take the last 5 (most recent);
  });
  
  displayEvents = computed(() => {
    return this.recentEvents()
      .sort((a, b) => new Date(a.timestamp).getTime() - new Date(b.timestamp).getTime()) // Sort chronologically (oldest first)
      .slice(-5); // Take the last 5 (most recent)
  });

  // Computed value for showing support section
  showSupportSection = computed(() => {
    return this.generalConfig()?.displaySupportBanner ?? false;
  });

  ngOnInit() {
    this.loadConfigurations();
    this.initializeHub();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadConfigurations(): void {
    // Load general configuration
    this.configurationService.getGeneralConfig()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (config) => {
          this.generalConfig.set(config);
        },
        error: (error) => {
          console.error('Failed to load general configuration:', error);
        }
      });
  }

  private initializeHub(): void {
    // Connect to unified hub
    this.appHubService.startConnection()
      .catch((error: Error) => console.error('Failed to connect to app hub:', error));

    // Subscribe to logs
    this.appHubService.getLogs()
      .pipe(takeUntil(this.destroy$))
      .subscribe((logs: LogEntry[]) => {
        this.recentLogs.set(logs);
      });

    // Subscribe to events
    this.appHubService.getEvents()
      .pipe(takeUntil(this.destroy$))
      .subscribe((events: AppEvent[]) => {
        this.recentEvents.set(events);
      });

    // Subscribe to connection status
    this.appHubService.getConnectionStatus()
      .pipe(takeUntil(this.destroy$))
      .subscribe((status: boolean) => {
        this.connected.set(status);
      });
  }

  // Log-related methods
  getLogIcon(level: string): string {
    const normalizedLevel = level?.toLowerCase() || '';
    
    switch (normalizedLevel) {
      case 'error':
      case 'fatal':
      case 'critical':
        return 'pi pi-times-circle';
      case 'warning':
        return 'pi pi-exclamation-triangle';
      case 'information':
      case 'info':
        return 'pi pi-info-circle';
      case 'debug':
      case 'trace':
      case 'verbose':
        return 'pi pi-code';
      default:
        return 'pi pi-circle';
    }
  }

  getLogIconClass(level: string): string {
    const normalizedLevel = level?.toLowerCase() || '';
    
    switch (normalizedLevel) {
      case 'error':
      case 'fatal':
      case 'critical':
        return 'log-icon-error';
      case 'warning':
        return 'log-icon-warning';
      case 'information':
      case 'info':
        return 'log-icon-info';
      case 'debug':
      case 'trace':
      case 'verbose':
        return 'log-icon-debug';
      default:
        return 'log-icon-default';
    }
  }

  getLogSeverity(level: string): string {
    const normalizedLevel = level?.toLowerCase() || '';
    
    switch (normalizedLevel) {
      case 'error':
      case 'fatal':
      case 'critical':
        return 'danger';
      case 'warning':
        return 'warn';
      case 'information':
      case 'info':
        return 'info';
      case 'debug':
      case 'trace':
      case 'verbose':
        return 'success';
      default:
        return 'secondary';
    }
  }

  // Event-related methods
  getEventIcon(eventType: string): string {
    const normalizedType = eventType?.toLowerCase() || '';
    
    if (normalizedType.includes('strike')) {
      return 'pi pi-bolt';
    }
    
    switch (normalizedType) {
      case 'downloadingmetadatastrike':
      case 'failedimportstrike':
      case 'stalledstrike':
      case 'slowspeedstrike':
      case 'slowtimestrike':
        return 'pi pi-bolt';
      case 'downloadcleaned':
        return 'pi pi-download';
      case 'queueitemdeleted':
        return 'pi pi-trash';
      case 'categorychanged':
        return 'pi pi-tag';
      default:
        return 'pi pi-circle';
    }
  }

  getEventIconClass(eventType: string, severity: string): string {
    const normalizedSeverity = severity?.toLowerCase() || '';
    const normalizedType = eventType?.toLowerCase() || '';
    
    // Strike events get special coloring based on severity
    if (normalizedType.includes('strike')) {
      switch (normalizedSeverity) {
        case 'error':
          return 'event-icon-strike-error';
        case 'warning':
          return 'event-icon-strike-warning';
        default:
          return 'event-icon-strike';
      }
    }
    
    // Other events get standard severity coloring
    switch (normalizedSeverity) {
      case 'error':
      case 'important':
        return 'event-icon-error';
      case 'warning':
        return 'event-icon-warning';
      case 'information':
        return 'event-icon-info';
      default:
        return 'event-icon-default';
    }
  }

  getEventSeverity(severity: string): string {
    const normalizedSeverity = severity?.toLowerCase() || '';
    
    switch (normalizedSeverity) {
      case 'error':
        return 'danger';
      case 'warning':
        return 'warn';
      case 'information':
        return 'info';
      case 'important':
        return 'warn';
      default:
        return 'secondary';
    }
  }

  // Utility methods
  truncateMessage(message: string, maxLength = 80): string {
    if (message.length <= maxLength) {
      return message;
    }
    return message.substring(0, maxLength) + '...';
  }

  formatEventType(eventType: string): string {
    // Convert PascalCase to readable format
    return eventType.replace(/([A-Z])/g, ' $1').trim();
  }
}
