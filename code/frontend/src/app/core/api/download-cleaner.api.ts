import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { DownloadCleanerConfig } from '@shared/models/download-cleaner-config.model';

@Injectable({ providedIn: 'root' })
export class DownloadCleanerApi {
  private http = inject(HttpClient);

  getConfig(): Observable<DownloadCleanerConfig> {
    return this.http.get<DownloadCleanerConfig>('/api/configuration/download_cleaner');
  }

  updateConfig(config: Partial<DownloadCleanerConfig>): Observable<void> {
    return this.http.put<void>('/api/configuration/download_cleaner', config);
  }
}
