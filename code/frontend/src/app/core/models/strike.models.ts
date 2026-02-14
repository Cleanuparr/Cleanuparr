export interface DownloadItemStrikes {
  downloadItemId: string;
  downloadId: string;
  title: string;
  isMarkedForRemoval: boolean;
  isRemoved: boolean;
  isReturning: boolean;
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
}

export interface RecentStrike {
  id: string;
  type: string;
  createdAt: string;
  downloadId: string;
  title: string;
}

export interface StrikeFilter {
  page?: number;
  pageSize?: number;
  search?: string;
  type?: string;
}
