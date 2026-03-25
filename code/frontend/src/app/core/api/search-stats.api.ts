import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import type { SearchStatsSummary, SearchEvent } from '@core/models/search-stats.models';
import type { PaginatedResult } from '@core/models/pagination.model';

@Injectable({ providedIn: 'root' })
export class SearchStatsApi {
  private http = inject(HttpClient);

  getSummary(): Observable<SearchStatsSummary> {
    return this.http.get<SearchStatsSummary>('/api/seeker/search-stats/summary');
  }

  getEvents(page = 1, pageSize = 50, instanceId?: string, cycleId?: string, search?: string): Observable<PaginatedResult<SearchEvent>> {
    const params: Record<string, string | number> = { page, pageSize };
    if (instanceId) params['instanceId'] = instanceId;
    if (cycleId) params['cycleId'] = cycleId;
    if (search) params['search'] = search;
    return this.http.get<PaginatedResult<SearchEvent>>('/api/seeker/search-stats/events', { params });
  }
}
