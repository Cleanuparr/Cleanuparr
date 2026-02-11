export interface SignalRHubConfig {
  hubUrl: string;
  maxReconnectAttempts: number;
  reconnectDelayMs: number;
  bufferSize: number;
  healthCheckIntervalMs: number;
}

export interface LogEntry {
  timestamp: Date;
  level: string;
  message: string;
  exception?: string;
  category?: string;
  jobName?: string;
  instanceName?: string;
}
