export interface AppEvent {
  id: string;
  timestamp: Date;
  eventType: string;
  message: string;
  data?: string;
  severity: string;
  trackingId?: string;
}

export interface ManualEvent {
  id: string;
  timestamp: Date;
  message: string;
  data?: string;
  severity: string;
  isResolved: boolean;
}

export interface EventStats {
  totalEvents: number;
  eventsBySeverity: { severity: string; count: number }[];
  eventsByType: { eventType: string; count: number }[];
  recentEventsCount: number;
}

export interface ManualEventStats {
  totalEvents: number;
  unresolvedEvents: number;
  resolvedEvents: number;
  eventsBySeverity: { severity: string; count: number }[];
  unresolvedBySeverity: { severity: string; count: number }[];
}

export interface EventFilter {
  page?: number;
  pageSize?: number;
  severity?: string;
  eventType?: string;
  fromDate?: string;
  toDate?: string;
  search?: string;
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
