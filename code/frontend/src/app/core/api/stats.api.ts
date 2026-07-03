import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { StatsV2Response, StatsWindow, TimelineBucket, TimelineMetric } from '@core/models/stats.models';

@Injectable({ providedIn: 'root' })
export class StatsApi {
  private http = inject(HttpClient);

  getStats(window: StatsWindow): Observable<StatsV2Response> {
    return this.http.get<StatsV2Response>('/api/v2/stats', {
      params: new HttpParams().set('window', window),
    });
  }

  getTimeline(metric: TimelineMetric, window: StatsWindow): Observable<TimelineBucket[]> {
    return this.http.get<TimelineBucket[]>('/api/v2/stats/timeline', {
      params: new HttpParams().set('metric', metric).set('window', window).set('bucket', 'day'),
    });
  }
}
