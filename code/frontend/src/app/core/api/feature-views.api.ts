import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface FeatureViewsResponse {
  /** Account creation timestamp (ISO) — the anchor for "new feature" detection. */
  createdAt: string;
  /** Map of feature id to the ISO timestamp the user first saw it. */
  views: Record<string, string>;
}

@Injectable({ providedIn: 'root' })
export class FeatureViewsApi {
  private http = inject(HttpClient);

  record(featureIds: string[]): Observable<FeatureViewsResponse> {
    return this.http.post<FeatureViewsResponse>('/api/account/feature-views', { featureIds });
  }
}
