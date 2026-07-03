export type StatsWindow = '24h' | '7d' | '30d' | '1y';

export type TimelineMetric = 'strikesIssued' | 'recovered' | 'removed' | 'malwareBlocked' | 'events';

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
    byType: Record<string, unknown>;
  };
  window: string;
  generatedAt: string;
}

export interface TimelineBucket {
  date: string;
  count: number;
}
