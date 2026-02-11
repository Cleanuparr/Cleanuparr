import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { BlacklistSyncConfig } from '@shared/models/blacklist-sync-config.model';

@Injectable({ providedIn: 'root' })
export class BlacklistSyncApi {
  private http = inject(HttpClient);

  getConfig(): Observable<BlacklistSyncConfig> {
    return this.http.get<BlacklistSyncConfig>('/api/configuration/blacklist_sync');
  }

  updateConfig(config: Partial<BlacklistSyncConfig>): Observable<void> {
    return this.http.put<void>('/api/configuration/blacklist_sync', config);
  }
}
