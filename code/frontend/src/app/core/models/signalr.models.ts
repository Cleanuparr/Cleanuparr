export interface LogEntry {
  timestamp: Date;
  level: string;
  message: string;
  exception?: string;
  category?: string;
  jobName?: string;
  instanceName?: string;
  downloadClientType?: string;
  downloadClientName?: string;
  jobRunId?: string;
}
