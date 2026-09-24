import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { DownloadItemStrikes, StrikeFilter } from '@core/models/strike.models';
import { PaginatedResult } from '@core/models/pagination.model';

@Injectable({ providedIn: 'root' })
export class StrikesApi {
  private http = inject(HttpClient);

  getStrikes(filter?: StrikeFilter): Observable<PaginatedResult<DownloadItemStrikes>> {
    let params = new HttpParams();
    if (filter) {
      if (filter.page) params = params.set('page', filter.page);
      if (filter.pageSize) params = params.set('pageSize', filter.pageSize);
      if (filter.search) params = params.set('search', filter.search);
      if (filter.type) params = params.set('type', filter.type);
    }
    return this.http.get<PaginatedResult<DownloadItemStrikes>>('/api/strikes', { params });
  }

  getStrikeTypes(): Observable<string[]> {
    return this.http.get<string[]>('/api/strikes/types');
  }

  deleteStrikesForItem(downloadItemId: string): Observable<void> {
    return this.http.delete<void>(`/api/strikes/${downloadItemId}`);
  }
}
