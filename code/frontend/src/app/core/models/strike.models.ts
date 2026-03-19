export interface DownloadItemStrikes {
  downloadItemId: string;
  downloadId: string;
  title: string;
  isMarkedForRemoval: boolean;
  isRemoved: boolean;
  isReturning: boolean;
  hasDryRunStrikes: boolean;
  totalStrikes: number;
  strikesByType: Record<string, number>;
  latestStrikeAt: string;
  firstStrikeAt: string;
  strikes: StrikeDetail[];
}

export interface StrikeDetail {
  id: string;
  type: string;
  createdAt: string;
  lastDownloadedBytes: number | null;
  jobRunId: string;
  isDryRun: boolean;
}

export interface RecentStrike {
  id: string;
  type: string;
  createdAt: string;
  downloadId: string;
  title: string;
  isDryRun: boolean;
}

export interface StrikeFilter {
  page?: number;
  pageSize?: number;
  search?: string;
  type?: string;
}
