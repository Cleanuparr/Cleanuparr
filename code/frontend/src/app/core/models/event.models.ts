export interface AppEvent {
  id: string;
  timestamp: Date;
  eventType: string;
  message: string;
  severity: string;
  trackingId?: string;
  strikeId?: string;
  jobRunId?: string;
  instanceType?: string;
  instanceUrl?: string;
  downloadClientType?: string;
  downloadClientName?: string;
  isDryRun: boolean;

  // Typed event payload (replaces the old JSON `data` blob)
  itemTitle?: string;
  itemHash?: string;
  strikeCount?: number;
  failedImportReasons?: string[];
  deleteReason?: string;
  removeFromClient?: boolean;
  cleanReason?: string;
  cleanedCategory?: string;
  seedRatio?: number;
  seedingTimeHours?: number;
  oldCategory?: string;
  newCategory?: string;
  isCategoryTag?: boolean;
  searchType?: string;
  searchReason?: string;
  grabbedItems?: string[];
}

export interface ManualEvent {
  id: string;
  timestamp: Date;
  message: string;
  severity: string;
  isResolved: boolean;
  jobRunId?: string;
  instanceType?: string;
  instanceUrl?: string;
  downloadClientType?: string;
  downloadClientName?: string;
  isDryRun: boolean;

  // Typed event payload (replaces the old JSON `data` blob)
  itemTitle?: string;
  itemHash?: string;
  strikeCount?: number;
}

export interface EventTypeTimelineBucket {
  date: string;
  counts: Record<string, number>;
}

export interface EventTypeTimelineResponse {
  types: string[];
  buckets: EventTypeTimelineBucket[];
}

export interface EventFilter {
  page?: number;
  pageSize?: number;
  severity?: string;
  eventType?: string;
  fromDate?: string;
  toDate?: string;
  search?: string;
  jobRunId?: string;
}

export interface ManualEventFilter {
  page?: number;
  pageSize?: number;
  isResolved?: boolean;
  severity?: string;
  fromDate?: string;
  toDate?: string;
  search?: string;
}
