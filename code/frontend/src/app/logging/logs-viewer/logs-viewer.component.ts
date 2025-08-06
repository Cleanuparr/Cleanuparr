import { Component, OnInit, OnDestroy, signal, computed, inject, ViewChild } from '@angular/core';
import { DatePipe, NgFor, NgIf } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, takeUntil, debounceTime, distinctUntilChanged } from 'rxjs';
import { Clipboard } from '@angular/cdk/clipboard';

// PrimeNG Imports
import { TableModule } from 'primeng/table';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import { CardModule } from 'primeng/card';
import { ToolbarModule } from 'primeng/toolbar';
import { TooltipModule } from 'primeng/tooltip';
import { ProgressSpinnerModule } from 'primeng/progressspinner';

// Services & Models
import { AppHubService } from '../../core/services/app-hub.service';
import { LogEntry } from '../../core/models/signalr.models';
import { MenuModule } from 'primeng/menu';
import { MenuItem } from 'primeng/api';

@Component({
  selector: 'app-logs-viewer',
  standalone: true,
  imports: [
    NgFor,
    NgIf,
    DatePipe,
    FormsModule,
    TableModule,
    InputTextModule,
    ButtonModule,
    SelectModule,
    TagModule,
    CardModule,
    ToolbarModule,
    TooltipModule,
    ProgressSpinnerModule,
    MenuModule
  ],
  providers: [AppHubService],
  templateUrl: './logs-viewer.component.html',
  styleUrl: './logs-viewer.component.scss'
})
export class LogsViewerComponent implements OnInit, OnDestroy {
  private appHubService = inject(AppHubService);
  private destroy$ = new Subject<void>();
  private clipboard = inject(Clipboard);
  private search$ = new Subject<string>();

  @ViewChild('exportMenu') exportMenu: any;
  
  // Signals for reactive state
  logs = signal<LogEntry[]>([]);
  isConnected = signal<boolean>(false);
  expandedLogs: { [key: number]: boolean } = {};
  
  // Filter state
  levelFilter = signal<string | null>(null);
  categoryFilter = signal<string | null>(null);
  searchFilter = signal<string>('');

  // Export menu items
  exportMenuItems: MenuItem[] = [
    { label: 'Export as JSON', icon: 'pi pi-file', command: () => this.exportAsJson() },
    { label: 'Export as CSV', icon: 'pi pi-file-excel', command: () => this.exportAsCsv() },
    { label: 'Export as Text', icon: 'pi pi-file-o', command: () => this.exportAsText() }
  ];

  // Computed values
  filteredLogs = computed(() => {
    let filtered = this.logs();
    
    if (this.levelFilter()) {
      filtered = filtered.filter(log => log.level === this.levelFilter());
    }
    
    if (this.categoryFilter()) {
      filtered = filtered.filter(log => log.category === this.categoryFilter());
    }
    
    if (this.searchFilter) {
      const search = this.searchFilter().toLowerCase();
      filtered = filtered.filter(log => 
        log.message.toLowerCase().includes(search) ||
        (log.exception && log.exception.toLowerCase().includes(search)));
    }
    
    // Sort by timestamp descending (newest first)
    return filtered.sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime());
  });
  
  levels = computed(() => {
    const uniqueLevels = [...new Set(this.logs().map(log => log.level))];
    return uniqueLevels.map(level => ({ label: level, value: level }));
  });
  
  categories = computed(() => {
    const uniqueCategories = [...new Set(this.logs().map(log => log.category).filter(Boolean))];
    return uniqueCategories.map(category => ({ label: category, value: category }));
  });
  
  constructor() {}
  
  ngOnInit(): void {
    // Connect to SignalR hub
    this.appHubService.startConnection()
      .catch((error: Error) => console.error('Failed to connect to app hub:', error));
    
    // Subscribe to logs
    this.appHubService.getLogs()
      .pipe(takeUntil(this.destroy$))
      .subscribe((logs: LogEntry[]) => {
        this.logs.set(logs);
      });
    
    // Subscribe to connection status
    this.appHubService.getLogsConnectionStatus()
      .pipe(takeUntil(this.destroy$))
      .subscribe((status: boolean) => {
        this.isConnected.set(status);
      });
      
    // Setup search debounce (300ms)
    this.search$
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        takeUntil(this.destroy$)
      )
      .subscribe(searchText => {
        this.searchFilter.set(searchText);
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
  
  onLevelFilterChange(level: string): void {
    this.levelFilter.set(level);
  }
  
  onCategoryFilterChange(category: string): void {
    this.categoryFilter.set(category);
  }
  
  onSearchChange(event: Event): void {
    const searchText = (event.target as HTMLInputElement).value;
    this.search$.next(searchText);
  }
  
  clearFilters(): void {
    this.levelFilter.set(null);
    this.categoryFilter.set(null);
    this.searchFilter.set('');
  }
  
  getSeverity(level: string): string {
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
        return 'info';
    }
  }
  
  refresh(): void {
    this.appHubService.requestRecentLogs();
  }
  
  hasJobInfo(): boolean {
    return this.logs().some(log => log.jobName);
  }
  
  hasInstanceInfo(): boolean {
    return this.logs().some(log => log.instanceName);
  }

  /**
   * Toggle expansion of a log entry
   */
  toggleLogExpansion(index: number, event?: Event): void {
    if (event) {
      event.stopPropagation();
    }
    this.expandedLogs[index] = !this.expandedLogs[index];
  }
  
  /**
   * Copy a specific log entry to clipboard
   */
  copyLogEntry(log: LogEntry, event: Event): void {
    event.stopPropagation();
    
    const timestamp = new Date(log.timestamp).toISOString();
    let content = `[${timestamp}] [${log.level}] ${log.category ? `[${log.category}] ` : ''}${log.message}`;
    
    if (log.exception) {
      content += `\n${log.exception}`;
    }
    
    this.clipboard.copy(content);
  }
  
  /**
   * Copy all filtered logs to clipboard
   */
  copyLogs(): void {
    const logs = this.filteredLogs();
    if (logs.length === 0) return;
    
    const content = logs.map(log => {
      const timestamp = new Date(log.timestamp).toISOString();
      let entry = `[${timestamp}] [${log.level}] ${log.category ? `[${log.category}] ` : ''}${log.message}`;
      
      if (log.exception) {
        entry += `\n${log.exception}`;
      }
      
      return entry;
    }).join('\n');
    
    this.clipboard.copy(content);
  }
  
  /**
   * Export logs menu trigger
   */
  exportLogs(event?: MouseEvent): void {
    if (event && this.exportMenuItems.length > 0 && this.exportMenu) {
      this.exportMenu.toggle(event);
    }
  }
  
  /**
   * Export logs as JSON
   */
  exportAsJson(): void {
    const logs = this.filteredLogs();
    if (logs.length === 0) return;
    
    const content = JSON.stringify(logs, null, 2);
    this.downloadFile(content, 'application/json', 'logs.json');
  }
  
  /**
   * Export logs as CSV
   */
  exportAsCsv(): void {
    const logs = this.filteredLogs();
    if (logs.length === 0) return;
    
    // CSV header
    let csv = 'Timestamp,Level,Category,Message,Exception,JobName,InstanceName\n';
    
    // CSV rows
    logs.forEach(log => {
      const timestamp = new Date(log.timestamp).toISOString();
      const level = log.level || '';
      const category = log.category ? `"${log.category.replace(/"/g, '""')}"` : '';
      const message = log.message ? `"${log.message.replace(/"/g, '""')}"` : '';
      const exception = log.exception ? `"${log.exception.replace(/"/g, '""').replace(/\n/g, ' ')}"` : '';
      const jobName = log.jobName ? `"${log.jobName.replace(/"/g, '""')}"` : '';
      const instanceName = log.instanceName ? `"${log.instanceName.replace(/"/g, '""')}"` : '';
      
      csv += `${timestamp},${level},${category},${message},${exception},${jobName},${instanceName}\n`;
    });
    
    this.downloadFile(csv, 'text/csv', 'logs.csv');
  }
  
  /**
   * Export logs as plain text
   */
  exportAsText(): void {
    const logs = this.filteredLogs();
    if (logs.length === 0) return;
    
    const content = logs.map(log => {
      const timestamp = new Date(log.timestamp).toISOString();
      let entry = `[${timestamp}] [${log.level}] ${log.category ? `[${log.category}] ` : ''}${log.message}`;
      
      if (log.exception) {
        entry += `\n${log.exception}`;
      }
      
      if (log.jobName) {
        entry += `\nJob: ${log.jobName}`;
      }
      
      if (log.instanceName) {
        entry += `\nInstance: ${log.instanceName}`;
      }
      
      return entry;
    }).join('\n\n');
    
    this.downloadFile(content, 'text/plain', 'logs.txt');
  }
  
  /**
   * Helper method to download a file
   */
  private downloadFile(content: string, contentType: string, filename: string): void {
    const blob = new Blob([content], { type: contentType });
    const url = URL.createObjectURL(blob);
    
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    document.body.appendChild(link); // Required for Firefox
    link.click();
    document.body.removeChild(link); // Clean up
    
    setTimeout(() => {
      URL.revokeObjectURL(url);
    }, 100);
  }
}
