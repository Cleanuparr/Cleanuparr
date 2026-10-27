export interface InstanceSearchStat {
  instanceId: string;
  instanceName: string;
  instanceType: string;
  instanceVersion: number;
  itemsTracked: number;
  totalSearchCount: number;
  lastSearchedAt: string | null;
  lastProcessedAt: string | null;
  currentCycleId: string | null;
  cycleItemsSearched: number;
  cycleItemsTotal: number;
  cycleStartedAt: string | null;
  cycleItemsRemaining: number;
  cycleCompletedAt: string | null;
  lastCycleStartedAt: string | null;
  lastCycleCompletedAt: string | null;
  funnel: InstanceFunnel | null;
}

export interface InstanceFunnel {
  libraryItems: number;
  releasedItems: number;
  monitoredItems: number;
  tagFilteredItems: number;
  eligibleUnits: number;
  queuedUnits: number;
  recentlyGrabbedUnits: number;
  restingUnits: number;
  searchedUnits: number;
  remainingUnits: number;
  countedAt: string;
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

export enum SeekerContentKind {
  Standard = 'Standard',
  Daily = 'Daily',
  Anime = 'Anime',
  Special = 'Special',
}

export enum SearchCommandStatus {
  Pending = 'Pending',
  Started = 'Started',
  Completed = 'Completed',
  Failed = 'Failed',
  TimedOut = 'TimedOut',
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
  contentKind: SeekerContentKind | null;
}

export enum SeekerExclusionReason {
  NotReleased = 'NotReleased',
  InGracePeriod = 'InGracePeriod',
  NotAvailable = 'NotAvailable',
  NotMonitored = 'NotMonitored',
  SkipTag = 'SkipTag',
  MissingIncludeTag = 'MissingIncludeTag',
  NothingToSearch = 'NothingToSearch',
  UpgradesDisabled = 'UpgradesDisabled',
  InQueue = 'InQueue',
  RecentlyGrabbed = 'RecentlyGrabbed',
  Resting = 'Resting',
  SearchedThisCycle = 'SearchedThisCycle',
}

export interface SeekerPreviewUnit {
  name: string;
  reason: SeekerSearchReason;
  contentKind: SeekerContentKind | null;
}

export interface SeekerPreviewExclusion {
  name: string;
  reason: SeekerExclusionReason;
  consecutiveMisses?: number;
  restCyclesLeft?: number;
  restUntil?: string;
}

export interface SeekerPreview {
  funnel: InstanceFunnel;
  cycleWaitingUntil: string | null;
  randomPick: boolean;
  nextUp: SeekerPreviewUnit[];
  excluded: SeekerPreviewExclusion[];
}
