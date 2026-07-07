import { DeleteReason } from '@shared/models/enums';

export type TimelineMetric = 'strikesIssued' | 'recovered' | 'removed' | 'malwareBlocked' | 'events';

export type TimelineBucketSize = 'hour' | 'day' | 'week' | 'month';

export const MALWARE_DELETE_REASONS: readonly DeleteReason[] = [
  DeleteReason.AllFilesBlocked,
  DeleteReason.AtLeastOneFileBlocked,
];

export interface JobTypeStats {
  total: number;
  completed: number;
  failed: number;
  lastRunAt?: string;
  nextRunAt?: string;
}

export interface StatsV2Response {
  events: {
    total: number;
    byType: Record<string, number>;
    bySeverity: Record<string, number>;
  };
  strikes: {
    total: number;
    byType: Record<string, number>;
    recovered: number;
  };
  removals: {
    total: number;
    byReason: Record<string, number>;
  };
  cleaned: {
    total: number;
    byReason: Record<string, number>;
  };
  searches: {
    total: number;
    completed: number;
    failed: number;
    grabbed: number;
    byReason: Record<string, number>;
  };
  jobs: {
    total: number;
    completed: number;
    failed: number;
    byType: Record<string, JobTypeStats>;
  };
  windowHours: number;
  generatedAt: string;
}

export interface TimelineBucket {
  date: string;
  count: number;
}
