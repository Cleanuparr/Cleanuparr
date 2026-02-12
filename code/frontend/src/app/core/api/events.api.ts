import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AppEvent, ManualEvent, EventStats, ManualEventStats, EventFilter, ManualEventFilter } from '@core/models/event.models';
import { PaginatedResult } from '@core/models/pagination.model';

@Injectable({ providedIn: 'root' })
export class EventsApi {
  private http = inject(HttpClient);

  getEvents(filter?: EventFilter): Observable<PaginatedResult<AppEvent>> {
    let params = new HttpParams();
    if (filter) {
      if (filter.page) params = params.set('page', filter.page);
      if (filter.pageSize) params = params.set('pageSize', filter.pageSize);
      if (filter.severity) params = params.set('severity', filter.severity);
      if (filter.eventType) params = params.set('eventType', filter.eventType);
      if (filter.fromDate) params = params.set('fromDate', filter.fromDate);
      if (filter.toDate) params = params.set('toDate', filter.toDate);
      if (filter.search) params = params.set('search', filter.search);
    }
    return this.http.get<PaginatedResult<AppEvent>>('/api/events', { params });
  }

  getEvent(id: string): Observable<AppEvent> {
    return this.http.get<AppEvent>(`/api/events/${id}`);
  }

  getEventsByTracking(trackingId: string): Observable<AppEvent[]> {
    return this.http.get<AppEvent[]>(`/api/events/tracking/${trackingId}`);
  }

  getEventStats(): Observable<EventStats> {
    return this.http.get<EventStats>('/api/events/stats');
  }

  getEventTypes(): Observable<string[]> {
    return this.http.get<string[]>('/api/events/types');
  }

  getSeverities(): Observable<string[]> {
    return this.http.get<string[]>('/api/events/severities');
  }

  cleanupOldEvents(retentionDays = 30): Observable<void> {
    return this.http.post<void>(`/api/events/cleanup?retentionDays=${retentionDays}`, {});
  }

  // Manual events
  getManualEvents(filter?: ManualEventFilter): Observable<PaginatedResult<ManualEvent>> {
    let params = new HttpParams();
    if (filter) {
      if (filter.page) params = params.set('page', filter.page);
      if (filter.pageSize) params = params.set('pageSize', filter.pageSize);
      if (filter.isResolved !== undefined) params = params.set('isResolved', filter.isResolved);
      if (filter.severity) params = params.set('severity', filter.severity);
      if (filter.fromDate) params = params.set('fromDate', filter.fromDate);
      if (filter.toDate) params = params.set('toDate', filter.toDate);
      if (filter.search) params = params.set('search', filter.search);
    }
    return this.http.get<PaginatedResult<ManualEvent>>('/api/manualevents', { params });
  }

  getManualEvent(id: string): Observable<ManualEvent> {
    return this.http.get<ManualEvent>(`/api/manualevents/${id}`);
  }

  resolveManualEvent(id: string): Observable<void> {
    return this.http.post<void>(`/api/manualevents/${id}/resolve`, {});
  }

  getManualEventStats(): Observable<ManualEventStats> {
    return this.http.get<ManualEventStats>('/api/manualevents/stats');
  }

  getManualEventSeverities(): Observable<string[]> {
    return this.http.get<string[]>('/api/manualevents/severities');
  }

  cleanupOldManualEvents(retentionDays = 30): Observable<{ deletedCount: number }> {
    return this.http.post<{ deletedCount: number }>(`/api/manualevents/cleanup?retentionDays=${retentionDays}`, {});
  }
}
