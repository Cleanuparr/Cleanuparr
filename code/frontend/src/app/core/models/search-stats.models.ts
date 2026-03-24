export interface InstanceSearchStat {
  instanceId: string;
  instanceName: string;
  instanceType: string;
  itemsTracked: number;
  totalSearchCount: number;
  lastSearchedAt: string | null;
  lastProcessedAt: string | null;
  currentRunId: string | null;
  cycleItemsSearched: number;
  cycleItemsTotal: number;
  cycleStartedAt: string | null;
}

export interface SearchStatsSummary {
  totalSearchesAllTime: number;
  searchesLast7Days: number;
  searchesLast30Days: number;
  uniqueItemsSearched: number;
  pendingReplacementSearches: number;
  enabledInstances: number;
  perInstanceStats: InstanceSearchStat[];
}

export interface SearchHistoryEntry {
  id: string;
  arrInstanceId: string;
  instanceName: string;
  instanceType: string;
  externalItemId: number;
  itemTitle: string;
  seasonNumber: number;
  lastSearchedAt: string;
  searchCount: number;
  totalSearchCount: number;
}

export enum SeekerSearchType {
  Proactive = 'Proactive',
  Replacement = 'Replacement',
}

export interface SearchEvent {
  id: string;
  timestamp: string;
  instanceName: string;
  instanceType: string | null;
  itemCount: number;
  items: string[];
  searchType: SeekerSearchType;
  searchStatus: string | null;
  completedAt: string | null;
  grabbedItems: unknown[] | null;
  cycleRunId: string | null;
  isDryRun: boolean;
}
