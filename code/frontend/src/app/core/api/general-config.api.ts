import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { GeneralConfig } from '@shared/models/general-config.model';

@Injectable({ providedIn: 'root' })
export class GeneralConfigApi {
  private http = inject(HttpClient);

  get(): Observable<GeneralConfig> {
    return this.http.get<GeneralConfig>('/api/configuration/general');
  }

  update(config: GeneralConfig): Observable<void> {
    return this.http.put<void>('/api/configuration/general', config);
  }
}
