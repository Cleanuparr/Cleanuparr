import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { SortDirection } from '@ui';

export { SortDirection };

export interface CfScoreStats {
  totalTracked: number;
  belowCutoff: number;
  atOrAboveCutoff: number;
  monitored: number;
  unmonitored: number;
  recentUpgrades: number;
  perInstanceStats: InstanceCfScoreStat[];
}

export interface InstanceCfScoreStat {
  instanceId: string;
  instanceName: string;
  instanceType: string;
  totalTracked: number;
  belowCutoff: number;
  atOrAboveCutoff: number;
  monitored: number;
  unmonitored: number;
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
  isMonitored: boolean;
  lastSyncedAt: string;
  lastUpgradedAt: string | null;
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
  qualityProfiles?: string[];
}

export enum CutoffFilter {
  All = 'All',
  Below = 'Below',
  Met = 'Met',
}

export enum MonitoredFilter {
  All = 'All',
  Monitored = 'Monitored',
  Unmonitored = 'Unmonitored',
}

export enum CfScoresSortBy {
  Title = 'Title',
  CurrentScore = 'CurrentScore',
  CutoffScore = 'CutoffScore',
  QualityProfile = 'QualityProfile',
  LastSyncedAt = 'LastSyncedAt',
  LastUpgradedAt = 'LastUpgradedAt',
}

export enum CfUpgradesSortBy {
  UpgradedAt = 'UpgradedAt',
  Title = 'Title',
  NewScore = 'NewScore',
  PreviousScore = 'PreviousScore',
  ScoreDelta = 'ScoreDelta',
  CutoffScore = 'CutoffScore',
}

export interface CfScoresQuery {
  page?: number;
  pageSize?: number;
  instanceId?: string;
  search?: string;
  sortBy?: CfScoresSortBy;
  sortDirection?: SortDirection;
  qualityProfile?: string;
  itemType?: string;
  cutoffFilter?: CutoffFilter;
  monitoredFilter?: MonitoredFilter;
}

export interface CfScoreUpgradesQuery {
  page?: number;
  pageSize?: number;
  instanceId?: string;
  days?: number;
  search?: string;
  sortBy?: CfUpgradesSortBy;
  sortDirection?: SortDirection;
}

@Injectable({ providedIn: 'root' })
export class CfScoreApi {
  private http = inject(HttpClient);

  getStats(): Observable<CfScoreStats> {
    return this.http.get<CfScoreStats>('/api/seeker/cf-scores/stats');
  }

  getRecentUpgrades(query: CfScoreUpgradesQuery = {}): Observable<CfScoreUpgradesResponse> {
    let params = new HttpParams()
      .set('page', String(query.page ?? 1))
      .set('pageSize', String(query.pageSize ?? 20));

    if (query.instanceId) params = params.set('instanceId', query.instanceId);
    if (query.days !== undefined) params = params.set('days', String(query.days));
    if (query.search) params = params.set('search', query.search);
    if (query.sortBy) params = params.set('sortBy', query.sortBy);
    if (query.sortDirection) params = params.set('sortDirection', query.sortDirection);

    return this.http.get<CfScoreUpgradesResponse>('/api/seeker/cf-scores/upgrades', { params });
  }

  getScores(query: CfScoresQuery = {}): Observable<CfScoreEntriesResponse> {
    let params = new HttpParams()
      .set('page', String(query.page ?? 1))
      .set('pageSize', String(query.pageSize ?? 50));

    if (query.search) params = params.set('search', query.search);
    if (query.instanceId) params = params.set('instanceId', query.instanceId);
    if (query.sortBy) params = params.set('sortBy', query.sortBy);
    if (query.sortDirection) params = params.set('sortDirection', query.sortDirection);
    if (query.qualityProfile) params = params.set('qualityProfile', query.qualityProfile);
    if (query.itemType) params = params.set('itemType', query.itemType);
    if (query.cutoffFilter && query.cutoffFilter !== CutoffFilter.All) params = params.set('cutoffFilter', query.cutoffFilter);
    if (query.monitoredFilter && query.monitoredFilter !== MonitoredFilter.All) params = params.set('monitoredFilter', query.monitoredFilter);

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
