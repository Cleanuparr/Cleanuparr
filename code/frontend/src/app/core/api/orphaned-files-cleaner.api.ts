import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { OrphanedFilesCleanerConfig, OrphanedFilesClientConfig } from '@shared/models/orphaned-files-cleaner-config.model';

@Injectable({ providedIn: 'root' })
export class OrphanedFilesCleanerApi {
  private http = inject(HttpClient);

  getConfig(): Observable<OrphanedFilesCleanerConfig> {
    return this.http.get<OrphanedFilesCleanerConfig>('/api/configuration/orphaned_files_cleaner');
  }

  updateConfig(config: Partial<OrphanedFilesCleanerConfig>): Observable<void> {
    return this.http.put<void>('/api/configuration/orphaned_files_cleaner', config);
  }

  getClientConfig(clientId: string): Observable<OrphanedFilesClientConfig | null> {
    return this.http.get<OrphanedFilesClientConfig | null>(`/api/configuration/orphaned_files_cleaner/clients/${clientId}`);
  }

  updateClientConfig(clientId: string, config: Partial<OrphanedFilesClientConfig>): Observable<OrphanedFilesClientConfig> {
    return this.http.put<OrphanedFilesClientConfig>(`/api/configuration/orphaned_files_cleaner/clients/${clientId}`, config);
  }
}
