import type { BadgeSeverity } from '@ui/badge/badge.component';
import { JobStatus, JobType } from '@shared/models/enums';
import { matchEnum } from './enum.util';

// Types left out here read well enough as-is.
const JOB_NAMES: Partial<Record<JobType, string>> = {
  [JobType.QueueCleaner]: 'Queue Cleaner',
  [JobType.MalwareBlocker]: 'Malware Blocker',
  [JobType.DownloadCleaner]: 'Download Cleaner',
  [JobType.BlacklistSynchronizer]: 'Blacklist Sync',
};

export function jobDisplayName(jobType: string): string {
  return JOB_NAMES[jobType as JobType] ?? jobType;
}

export function jobStatusSeverity(status: string): BadgeSeverity {
  switch (matchEnum(JobStatus, status)) {
    case JobStatus.Running:
      return 'info';
    case JobStatus.Complete:
    case JobStatus.Scheduled:
      return 'success';
    case JobStatus.Error:
      return 'error';
    case JobStatus.Paused:
      return 'warning';
    default:
      return 'default';
  }
}
