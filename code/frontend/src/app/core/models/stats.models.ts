export type TimelineMetric = 'strikesIssued' | 'recovered' | 'removed' | 'malwareBlocked' | 'events';

export interface JobTypeStats {
  totalRuns: number;
  completed: number;
  failed: number;
  lastRunAt?: string;
  nextRunAt?: string;
}

export interface StatsV2Response {
  events: {
    totalCount: number;
    byType: Record<string, number>;
    bySeverity: Record<string, number>;
  };
  strikes: {
    active: Record<string, number>;
    issued: number;
    recovered: number;
    removed: number;
  };
  malware: {
    blocked: number;
  };
  jobs: {
    totalRuns: number;
    completed: number;
    failed: number;
    byType: Record<string, JobTypeStats>;
  };
  generatedAt: string;
}

export interface TimelineBucket {
  date: string;
  count: number;
}
