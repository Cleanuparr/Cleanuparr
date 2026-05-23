import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  DownloadCleanerConfig,
  SeedingRule,
  UnlinkedConfigModel,
  OrphanedFilesCleanerConfig,
  OrphanedFilesClientConfig,
} from '@shared/models/download-cleaner-config.model';

@Injectable({ providedIn: 'root' })
export class DownloadCleanerApi {
  private http = inject(HttpClient);

  getConfig(): Observable<DownloadCleanerConfig> {
    return this.http.get<DownloadCleanerConfig>('/api/configuration/download_cleaner');
  }

  updateConfig(config: Partial<DownloadCleanerConfig>): Observable<void> {
    return this.http.put<void>('/api/configuration/download_cleaner', config);
  }

  // Seeding rules CRUD
  getSeedingRules(clientId: string): Observable<SeedingRule[]> {
    return this.http.get<SeedingRule[]>(`/api/seeding-rules/${clientId}`);
  }

  createSeedingRule(clientId: string, rule: Partial<SeedingRule>): Observable<SeedingRule> {
    return this.http.post<SeedingRule>(`/api/seeding-rules/${clientId}`, rule);
  }

  updateSeedingRule(id: string, rule: Partial<SeedingRule>): Observable<SeedingRule> {
    return this.http.put<SeedingRule>(`/api/seeding-rules/${id}`, rule);
  }

  deleteSeedingRule(id: string): Observable<void> {
    return this.http.delete<void>(`/api/seeding-rules/${id}`);
  }

  reorderSeedingRules(clientId: string, orderedIds: string[]): Observable<void> {
    return this.http.put<void>(`/api/seeding-rules/${clientId}/reorder`, { orderedIds });
  }

  // Unlinked config
  getUnlinkedConfig(clientId: string): Observable<UnlinkedConfigModel | null> {
    return this.http.get<UnlinkedConfigModel | null>(`/api/unlinked-config/${clientId}`);
  }

  updateUnlinkedConfig(clientId: string, config: Partial<UnlinkedConfigModel>): Observable<void> {
    return this.http.put<void>(`/api/unlinked-config/${clientId}`, config);
  }

  // Orphaned files config
  getOrphanedFilesConfig(): Observable<OrphanedFilesCleanerConfig> {
    return this.http.get<OrphanedFilesCleanerConfig>('/api/configuration/orphaned_files_cleaner');
  }

  updateOrphanedFilesConfig(config: Partial<OrphanedFilesCleanerConfig>): Observable<void> {
    return this.http.put<void>('/api/configuration/orphaned_files_cleaner', config);
  }

  getOrphanedFilesClientConfig(clientId: string): Observable<OrphanedFilesClientConfig | null> {
    return this.http.get<OrphanedFilesClientConfig | null>(`/api/configuration/orphaned_files_cleaner/clients/${clientId}`);
  }

  updateOrphanedFilesClientConfig(clientId: string, config: Partial<OrphanedFilesClientConfig>): Observable<OrphanedFilesClientConfig> {
    return this.http.put<OrphanedFilesClientConfig>(`/api/configuration/orphaned_files_cleaner/clients/${clientId}`, config);
  }
}
