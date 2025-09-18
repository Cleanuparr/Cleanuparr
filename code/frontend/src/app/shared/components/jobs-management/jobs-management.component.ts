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
import { MessagesModule } from 'primeng/messages';
import { ConfirmDialogModule } from 'primeng/confirmdialog';

// Services & Models
import { JobsService } from '../../../core/services/jobs.service';
import { NotificationService } from '../../../core/services/notification.service';
import { JobInfo, JobType, JobAction } from '../../../core/models/job.models';
import { ConfirmationService, Message } from 'primeng/api';

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
    MessagesModule,
    ConfirmDialogModule
  ],
  providers: [ConfirmationService],
  templateUrl: './jobs-management.component.html',
  styleUrl: './jobs-management.component.scss'
})
export class JobsManagementComponent implements OnInit, OnDestroy {
  private jobsService = inject(JobsService);
  private notificationService = inject(NotificationService);
  private confirmationService = inject(ConfirmationService);
  private destroy$ = new Subject<void>();

  // Signals for reactive state
  jobs = signal<JobInfo[]>([]);
  loading = signal<boolean>(false);
  messages = signal<Message[]>([]);

  // Job actions configuration
  jobActions: JobAction[] = [
    {
      label: 'Run Now',
      icon: 'pi pi-play',
      severity: 'success',
      action: (jobType: JobType) => this.triggerJob(jobType),
      disabled: (job: JobInfo) => job.status === 'Error'
    },
    {
      label: 'Pause',
      icon: 'pi pi-pause',
      severity: 'warn',
      action: (jobType: JobType) => this.pauseJob(jobType),
      disabled: (job: JobInfo) => job.status !== 'Running'
    },
    {
      label: 'Resume',
      icon: 'pi pi-play-circle',
      severity: 'info',
      action: (jobType: JobType) => this.resumeJob(jobType),
      disabled: (job: JobInfo) => job.status !== 'Paused'
    },
    {
      label: 'Stop',
      icon: 'pi pi-stop',
      severity: 'danger',
      action: (jobType: JobType) => this.stopJob(jobType),
      disabled: (job: JobInfo) => job.status === 'None' || job.status === 'Error'
    }
  ];

  ngOnInit() {
    this.loadJobs();
    // Set up auto-reload every 30 seconds
    setInterval(() => this.loadJobs(), 30000);
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadJobs(): void {
    this.loading.set(true);
    this.jobsService.getAllJobs()
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (jobs) => {
          this.jobs.set(jobs);
          this.clearMessages();
        },
        error: (error) => {
          console.error('Failed to load jobs:', error);
          this.addMessage('error', 'Failed to load jobs', error.message || 'Unknown error occurred');
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
              this.addMessage('success', 'Job Triggered', response.message || `${jobName} has been triggered for execution`);
              setTimeout(() => this.loadJobs(), 2000); // Reload after 2 seconds
            },
            error: (error) => {
              console.error('Failed to trigger job:', error);
              this.notificationService.showError(`Failed to trigger ${jobName}`);
              this.addMessage('error', 'Trigger Failed', error.error?.message || error.message || 'Unknown error occurred');
            }
          });
      }
    });
  }

  pauseJob(jobType: JobType): void {
    const jobName = this.getJobDisplayName(jobType);
    
    this.loading.set(true);
    this.jobsService.pauseJob(jobType)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (response) => {
          this.notificationService.showSuccess(`${jobName} paused successfully`);
          this.addMessage('success', 'Job Paused', response.message || `${jobName} has been paused`);
          this.loadJobs();
        },
        error: (error) => {
          console.error('Failed to pause job:', error);
          this.notificationService.showError(`Failed to pause ${jobName}`);
          this.addMessage('error', 'Pause Failed', error.error?.message || error.message || 'Unknown error occurred');
        }
      });
  }

  resumeJob(jobType: JobType): void {
    const jobName = this.getJobDisplayName(jobType);
    
    this.loading.set(true);
    this.jobsService.resumeJob(jobType)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (response) => {
          this.notificationService.showSuccess(`${jobName} resumed successfully`);
          this.addMessage('success', 'Job Resumed', response.message || `${jobName} has been resumed`);
          this.loadJobs();
        },
        error: (error) => {
          console.error('Failed to resume job:', error);
          this.notificationService.showError(`Failed to resume ${jobName}`);
          this.addMessage('error', 'Resume Failed', error.error?.message || error.message || 'Unknown error occurred');
        }
      });
  }

  stopJob(jobType: JobType): void {
    const jobName = this.getJobDisplayName(jobType);
    
    this.confirmationService.confirm({
      message: `Are you sure you want to stop ${jobName}?`,
      header: 'Stop Job',
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.loading.set(true);
        this.jobsService.stopJob(jobType)
          .pipe(
            takeUntil(this.destroy$),
            finalize(() => this.loading.set(false))
          )
          .subscribe({
            next: (response) => {
              this.notificationService.showSuccess(`${jobName} stopped successfully`);
              this.addMessage('success', 'Job Stopped', response.message || `${jobName} has been stopped`);
              this.loadJobs();
            },
            error: (error) => {
              console.error('Failed to stop job:', error);
              this.notificationService.showError(`Failed to stop ${jobName}`);
              this.addMessage('error', 'Stop Failed', error.error?.message || error.message || 'Unknown error occurred');
            }
          });
      }
    });
  }

  getJobDisplayName(jobType: JobType): string {
    switch (jobType) {
      case JobType.QueueCleaner:
        return 'Queue Cleaner';
      case JobType.MalwareBlocker:
        return 'Malware Blocker';
      case JobType.DownloadCleaner:
        return 'Download Cleaner';
      case JobType.BlacklistSynchronizer:
        return 'Blacklist Synchronizer';
      default:
        return jobType.toString();
    }
  }

  getStatusSeverity(status: string): string {
    switch (status.toLowerCase()) {
      case 'running':
      case 'normal':
        return 'success';
      case 'paused':
        return 'warn';
      case 'error':
        return 'danger';
      case 'complete':
        return 'info';
      case 'blocked':
        return 'warn';
      case 'none':
        return 'secondary';
      default:
        return 'secondary';
    }
  }

  formatDateTime(date?: Date): string {
    if (!date) return 'Never';
    return new Date(date).toLocaleString();
  }

  private addMessage(severity: 'success' | 'info' | 'warn' | 'error', summary: string, detail: string): void {
    const currentMessages = this.messages();
    this.messages.set([...currentMessages, { severity, summary, detail }]);
    
    // Auto-clear success messages after 5 seconds
    if (severity === 'success') {
      setTimeout(() => {
        const messages = this.messages().filter(m => !(m.severity === severity && m.summary === summary));
        this.messages.set(messages);
      }, 5000);
    }
  }

  private clearMessages(): void {
    this.messages.set([]);
  }

  onRefresh(): void {
    this.loadJobs();
  }
}