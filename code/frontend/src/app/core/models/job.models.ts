export interface JobInfo {
  name: string;
  status: string;
  schedule: string;
  nextRunTime?: Date;
  previousRunTime?: Date;
  jobType: string;
}

export enum JobType {
  QueueCleaner = 'QueueCleaner',
  MalwareBlocker = 'MalwareBlocker',
  DownloadCleaner = 'DownloadCleaner',
  BlacklistSynchronizer = 'BlacklistSynchronizer'
}

export interface JobSchedule {
  every: number;
  type: ScheduleType;
}

export enum ScheduleType {
  Minutes = 'Minutes',
  Hours = 'Hours',
  Days = 'Days'
}

export interface JobAction {
  label: string;
  icon: string;
  action: (jobType: JobType) => void;
  disabled?: (job: JobInfo) => boolean;
  severity?: 'primary' | 'secondary' | 'success' | 'info' | 'warn' | 'danger';
}