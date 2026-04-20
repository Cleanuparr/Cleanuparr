import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import type { SearchStatsSummary, SearchEvent } from '@core/models/search-stats.models';
import type { PaginatedResult } from '@core/models/pagination.model';

export enum SortDirection {
  Asc = 'Asc',
  Desc = 'Desc',
}

export enum SearchEventsSortBy {
  Timestamp = 'Timestamp',
  Title = 'Title',
  Status = 'Status',
  Type = 'Type',
}

export interface SearchEventsQuery {
  page?: number;
  pageSize?: number;
  instanceId?: string;
  cycleId?: string;
  search?: string;
  sortBy?: SearchEventsSortBy;
  sortDirection?: SortDirection;
  searchStatus?: string[];
  searchType?: string;
  searchReason?: string;
  grabbed?: boolean;
}

@Injectable({ providedIn: 'root' })
export class SearchStatsApi {
  private http = inject(HttpClient);

  getSummary(): Observable<SearchStatsSummary> {
    return this.http.get<SearchStatsSummary>('/api/seeker/search-stats/summary');
  }

  getEvents(query: SearchEventsQuery = {}): Observable<PaginatedResult<SearchEvent>> {
    let params = new HttpParams()
      .set('page', String(query.page ?? 1))
      .set('pageSize', String(query.pageSize ?? 50));

    if (query.instanceId) params = params.set('instanceId', query.instanceId);
    if (query.cycleId) params = params.set('cycleId', query.cycleId);
    if (query.search) params = params.set('search', query.search);
    if (query.sortBy) params = params.set('sortBy', query.sortBy);
    if (query.sortDirection) params = params.set('sortDirection', query.sortDirection);
    if (query.searchType) params = params.set('searchType', query.searchType);
    if (query.searchReason) params = params.set('searchReason', query.searchReason);
    if (query.grabbed !== undefined) params = params.set('grabbed', String(query.grabbed));

    if (query.searchStatus?.length) {
      for (const status of query.searchStatus) {
        params = params.append('searchStatus', status);
      }
    }

    return this.http.get<PaginatedResult<SearchEvent>>('/api/seeker/search-stats/events', { params });
  }
}
