import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { SeekerConfig, UpdateSeekerConfig } from '@shared/models/seeker-config.model';

@Injectable({ providedIn: 'root' })
export class SeekerApi {
  private http = inject(HttpClient);

  getConfig(): Observable<SeekerConfig> {
    return this.http.get<SeekerConfig>('/api/configuration/seeker');
  }

  updateConfig(config: UpdateSeekerConfig): Observable<void> {
    return this.http.put<void>('/api/configuration/seeker', config);
  }
}
