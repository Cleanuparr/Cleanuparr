import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { DownloadCleanerConfig, SeedingRule, UnlinkedConfigModel } from '@shared/models/download-cleaner-config.model';

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

  // Unlinked config
  getUnlinkedConfig(clientId: string): Observable<UnlinkedConfigModel | null> {
    return this.http.get<UnlinkedConfigModel | null>(`/api/unlinked-config/${clientId}`);
  }

  updateUnlinkedConfig(clientId: string, config: Partial<UnlinkedConfigModel>): Observable<void> {
    return this.http.put<void>(`/api/unlinked-config/${clientId}`, config);
  }
}
