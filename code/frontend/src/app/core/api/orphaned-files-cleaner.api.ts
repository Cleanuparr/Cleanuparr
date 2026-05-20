import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { OrphanedFilesCleanerConfig } from '@shared/models/orphaned-files-cleaner-config.model';

@Injectable({ providedIn: 'root' })
export class OrphanedFilesCleanerApi {
  private http = inject(HttpClient);

  getConfig(): Observable<OrphanedFilesCleanerConfig> {
    return this.http.get<OrphanedFilesCleanerConfig>('/api/configuration/orphaned_files_cleaner');
  }

  updateConfig(config: OrphanedFilesCleanerConfig): Observable<void> {
    return this.http.put<void>('/api/configuration/orphaned_files_cleaner', config);
  }
}
