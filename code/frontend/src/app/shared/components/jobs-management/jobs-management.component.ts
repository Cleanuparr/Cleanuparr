import { Component, OnInit, OnDestroy, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subject, takeUntil, finalize } from 'rxjs';

// PrimeNG Components
import { CardModule } from 'primeng/card';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { TableModule } from 'primeng/table';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { ConfirmDialogModule } from 'primeng/confirmdialog';

// Services & Models
import { JobsService } from '../../../core/services/jobs.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AppHubService } from '../../../core/services/app-hub.service';
import { JobInfo, JobType, JobAction } from '../../../core/models/job.models';
import { ConfirmationService } from 'primeng/api';

@Component({
  selector: 'app-jobs-management',
  standalone: true,
  imports: [
    CommonModule,
    CardModule,
    ButtonModule,
    TagModule,
    TooltipModule,
    TableModule,
    ProgressSpinnerModule,
    ConfirmDialogModule
  ],
  providers: [ConfirmationService],
  templateUrl: './jobs-management.component.html',
  styleUrl: './jobs-management.component.scss'
})
export class JobsManagementComponent implements OnInit, OnDestroy {
  private jobsService = inject(JobsService);
  private notificationService = inject(NotificationService);
  private appHubService = inject(AppHubService);
  private confirmationService = inject(ConfirmationService);
  private destroy$ = new Subject<void>();

  // Expose JobType for template
  JobType = JobType;

  // Signals for reactive state
  jobs = signal<JobInfo[]>([]);
  loading = signal<boolean>(false);
  connected = signal<boolean>(false);

  // Job actions configuration
  jobActions: JobAction[] = [
    {
      label: 'Run Now',
      icon: 'pi pi-play',
      severity: 'success',
      action: (jobType: JobType) => this.triggerJob(jobType),
      disabled: (job: JobInfo) => job.status === 'Error'
    }
  ];

  ngOnInit() {
    this.initializeJobsData();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initializeJobsData(): void {
    // Subscribe to connection status
    this.appHubService.getJobsConnectionStatus()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (connected) => {
          this.connected.set(connected);
        }
      });

    // Subscribe to real-time job updates via SignalR
    this.appHubService.getJobs()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (jobs) => {
          this.jobs.set(jobs);
          this.loading.set(false);
        },
        error: (error) => {
          console.error('Error receiving job updates:', error);
        }
      });
  }

  triggerJob(jobType: JobType): void {
    const jobName = this.getJobDisplayName(jobType);
    
    this.confirmationService.confirm({
      message: `Are you sure you want to trigger ${jobName} to run now?`,
      header: 'Trigger Job',
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: 'p-button-success',
      accept: () => {
        this.loading.set(true);
        this.jobsService.triggerJob(jobType)
          .pipe(
            takeUntil(this.destroy$),
            finalize(() => this.loading.set(false))
          )
          .subscribe({
            next: (response) => {
              this.notificationService.showSuccess(`${jobName} triggered successfully`);
              // Job status will be updated automatically via SignalR
            },
            error: (error) => {
              console.error('Failed to trigger job:', error);
              this.notificationService.showError(`Failed to trigger ${jobName}`);
            }
          });
      }
    });
  }

  getJobDisplayName(jobName: string): string {
    switch (jobName) {
      case 'QueueCleaner':
        return 'Queue Cleaner';
      case 'MalwareBlocker':
        return 'Malware Blocker';
      case 'DownloadCleaner':
        return 'Download Cleaner';
      case 'BlacklistSynchronizer':
        return 'Blacklist Synchronizer';
      default:
        return jobName;
    }
  }

  getJobTypeEnum(jobTypeString: string): JobType {
    return jobTypeString as JobType;
  }

  getStatusSeverity(status: string): string {
    switch (status.toLowerCase()) {
      case 'scheduled':
        return 'info';
      case 'running':
        return 'success';
      case 'paused':
        return 'warn';
      case 'error':
        return 'danger';
      case 'complete':
        return 'success';
      case 'not scheduled':
        return 'secondary';
      default:
        return 'secondary';
    }
  }

  formatDateTime(date?: Date): string {
    if (!date) return 'Never';
    return new Date(date).toLocaleString();
  }
}