export interface InstanceSearchStat {
  instanceId: string;
  instanceName: string;
  instanceType: string;
  itemsTracked: number;
  totalSearchCount: number;
  lastSearchedAt: string | null;
  lastProcessedAt: string | null;
  currentCycleId: string | null;
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

export enum SeekerSearchType {
  Proactive = 'Proactive',
  Replacement = 'Replacement',
}

export enum SeekerSearchReason {
  Missing = 'Missing',
  QualityCutoffNotMet = 'QualityCutoffNotMet',
  CustomFormatScoreBelowCutoff = 'CustomFormatScoreBelowCutoff',
  Replacement = 'Replacement',
}

export interface SearchEvent {
  id: string;
  timestamp: string;
  arrInstanceId: string | null;
  instanceType: string | null;
  itemTitle: string;
  searchType: SeekerSearchType;
  searchReason: string | null;
  searchStatus: string | null;
  completedAt: string | null;
  grabbedItems: string[] | null;
  cycleId: string | null;
  isDryRun: boolean;
}
