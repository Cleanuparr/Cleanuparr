import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ManualEvent } from '../models/event.models';
import { ApplicationPathService } from './base-path.service';

export interface PaginatedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface ManualEventFilter {
  page?: number;
  pageSize?: number;
  isResolved?: boolean;
  severity?: string;
  fromDate?: Date;
  toDate?: Date;
  search?: string;
}

export interface ManualEventStats {
  totalEvents: number;
  unresolvedEvents: number;
  resolvedEvents: number;
  eventsBySeverity: { severity: string; count: number }[];
  unresolvedBySeverity: { severity: string; count: number }[];
}

@Injectable({
  providedIn: 'root'
})
export class ManualEventsService {
  private readonly http = inject(HttpClient);
  private readonly applicationPathService = inject(ApplicationPathService);
  private readonly baseUrl = this.applicationPathService.buildApiUrl('/manualevents');

  /**
   * Get manual events with pagination and filtering
   */
  getManualEvents(filter?: ManualEventFilter): Observable<PaginatedResult<ManualEvent>> {
    let params = new HttpParams();

    if (filter) {
      if (filter.page !== undefined) {
        params = params.set('page', filter.page.toString());
      }
      if (filter.pageSize !== undefined) {
        params = params.set('pageSize', filter.pageSize.toString());
      }
      if (filter.isResolved !== undefined) {
        params = params.set('isResolved', filter.isResolved.toString());
      }
      if (filter.severity) {
        params = params.set('severity', filter.severity);
      }
      if (filter.fromDate) {
        params = params.set('fromDate', filter.fromDate.toISOString());
      }
      if (filter.toDate) {
        params = params.set('toDate', filter.toDate.toISOString());
      }
      if (filter.search) {
        params = params.set('search', filter.search);
      }
    }

    return this.http.get<PaginatedResult<ManualEvent>>(this.baseUrl, { params });
  }

  /**
   * Get a specific manual event by ID
   */
  getManualEvent(id: string): Observable<ManualEvent> {
    return this.http.get<ManualEvent>(`${this.baseUrl}/${id}`);
  }

  /**
   * Mark a manual event as resolved
   */
  resolveManualEvent(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/resolve`, {});
  }

  /**
   * Get manual event statistics
   */
  getManualEventStats(): Observable<ManualEventStats> {
    return this.http.get<ManualEventStats>(`${this.baseUrl}/stats`);
  }

  /**
   * Get available severities
   */
  getSeverities(): Observable<string[]> {
    return this.http.get<string[]>(`${this.baseUrl}/severities`);
  }

  /**
   * Trigger cleanup of old resolved events
   */
  cleanupOldResolvedEvents(retentionDays: number = 30): Observable<{ deletedCount: number }> {
    const params = new HttpParams().set('retentionDays', retentionDays.toString());
    return this.http.post<{ deletedCount: number }>(`${this.baseUrl}/cleanup`, {}, { params });
  }
}
