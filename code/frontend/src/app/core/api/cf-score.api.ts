import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface CfScoreStats {
  totalTracked: number;
  belowCutoff: number;
  atOrAboveCutoff: number;
  recentUpgrades: number;
  averageScore: number;
  perInstanceStats: InstanceCfScoreStat[];
}

export interface InstanceCfScoreStat {
  instanceId: string;
  instanceName: string;
  instanceType: string;
  totalTracked: number;
  belowCutoff: number;
  atOrAboveCutoff: number;
  recentUpgrades: number;
}

export interface CfScoreUpgrade {
  arrInstanceId: string;
  externalItemId: number;
  episodeId: number;
  itemType: string;
  title: string;
  previousScore: number;
  newScore: number;
  cutoffScore: number;
  upgradedAt: string;
}

export interface CfScoreUpgradesResponse {
  items: CfScoreUpgrade[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface CfScoreEntry {
  id: string;
  arrInstanceId: string;
  externalItemId: number;
  episodeId: number;
  itemType: string;
  title: string;
  fileId: number;
  currentScore: number;
  cutoffScore: number;
  qualityProfileName: string;
  isBelowCutoff: boolean;
  lastSyncedAt: string;
}

export interface CfScoreEntriesResponse {
  items: CfScoreEntry[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface CfScoreHistoryEntry {
  score: number;
  cutoffScore: number;
  recordedAt: string;
}

export interface CfScoreHistoryResponse {
  entries: CfScoreHistoryEntry[];
}

export interface CfScoreInstance {
  id: string;
  name: string;
  itemType: string;
}

@Injectable({ providedIn: 'root' })
export class CfScoreApi {
  private http = inject(HttpClient);

  getStats(): Observable<CfScoreStats> {
    return this.http.get<CfScoreStats>('/api/seeker/cf-scores/stats');
  }

  getRecentUpgrades(page = 1, pageSize = 5): Observable<CfScoreUpgradesResponse> {
    return this.http.get<CfScoreUpgradesResponse>('/api/seeker/cf-scores/upgrades', {
      params: { page, pageSize },
    });
  }

  getScores(page = 1, pageSize = 50, search?: string, instanceId?: string, sortBy?: string, hideMet?: boolean): Observable<CfScoreEntriesResponse> {
    const params: Record<string, string | number | boolean> = { page, pageSize };
    if (search) params['search'] = search;
    if (instanceId) params['instanceId'] = instanceId;
    if (sortBy) params['sortBy'] = sortBy;
    if (hideMet) params['hideMet'] = true;
    return this.http.get<CfScoreEntriesResponse>('/api/seeker/cf-scores', { params });
  }

  getInstances(): Observable<{ instances: CfScoreInstance[] }> {
    return this.http.get<{ instances: CfScoreInstance[] }>('/api/seeker/cf-scores/instances');
  }

  getItemHistory(instanceId: string, itemId: number, episodeId = 0): Observable<CfScoreHistoryResponse> {
    return this.http.get<CfScoreHistoryResponse>(
      `/api/seeker/cf-scores/${instanceId}/${itemId}/history`,
      { params: { episodeId } },
    );
  }
}
