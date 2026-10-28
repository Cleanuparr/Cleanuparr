import type { BadgeSeverity } from '@ui/badge/badge.component';
import { SeekerSearchType, SeekerSearchReason, SearchCommandStatus, SeekerExclusionReason } from '@core/models/search-stats.models';
import type { InstanceFunnel, InstanceSearchStat } from '@core/models/search-stats.models';

export function formatSearchReason(reason: string): string {
  switch (reason) {
    case SeekerSearchReason.Missing: return 'Missing';
    case SeekerSearchReason.QualityCutoffNotMet: return 'Cutoff Unmet';
    case SeekerSearchReason.CustomFormatScoreBelowCutoff: return 'CF Below Cutoff';
    case SeekerSearchReason.Replacement: return 'Replacement';
    default: return reason;
  }
}

export function searchReasonSeverity(reason: string): BadgeSeverity {
  switch (reason) {
    case SeekerSearchReason.Missing: return 'error';
    case SeekerSearchReason.QualityCutoffNotMet: return 'warning';
    case SeekerSearchReason.CustomFormatScoreBelowCutoff: return 'warning';
    case SeekerSearchReason.Replacement: return 'info';
    default: return 'default';
  }
}

export function searchTypeSeverity(type: SeekerSearchType): 'info' | 'warning' {
  return type === SeekerSearchType.Replacement ? 'warning' : 'info';
}

export function searchStatusSeverity(status: string): BadgeSeverity {
  switch (status) {
    case SearchCommandStatus.Completed: return 'success';
    case SearchCommandStatus.Failed: return 'error';
    case SearchCommandStatus.TimedOut: return 'warning';
    case SearchCommandStatus.Started: return 'info';
    default: return 'default';
  }
}

export function formatGrabbedItems(items: string[]): string {
  return items.join(', ');
}

/** Percentage of the current search cycle completed for an instance. */
export function cycleProgress(inst: InstanceSearchStat): number {
  if (!inst.cycleItemsTotal) return 0;
  return Math.min(100, Math.round((inst.cycleItemsSearched / inst.cycleItemsTotal) * 100));
}

export function instanceHealthWarning(stat: InstanceSearchStat): string | null {
  if (!stat.lastSearchedAt && stat.totalSearchCount === 0) {
    return 'Never searched';
  }
  return null;
}

/** Renders the age of a running cycle down to the largest non-zero unit. */
export function formatCycleDuration(cycleStartedAt: string): string {
  return formatDurationBetween(cycleStartedAt, new Date().toISOString());
}

/** Renders the span between two timestamps down to the largest non-zero unit. */
export function formatDurationBetween(start: string, end: string): string {
  const diffMs = new Date(end).getTime() - new Date(start).getTime();
  const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));
  const diffHours = Math.floor((diffMs % (1000 * 60 * 60 * 24)) / (1000 * 60 * 60));

  if (diffDays > 0) {
    return `${diffDays}d ${diffHours}h`;
  }
  if (diffHours > 0) {
    return `${diffHours}h`;
  }
  const diffMinutes = Math.floor((diffMs % (1000 * 60 * 60)) / (1000 * 60));
  return `${diffMinutes}m`;
}

export interface FunnelStage {
  label: string;
  value: number;
}

/**
 * Splits the funnel into the stages that narrow the library down to units needing a search,
 * and the split of those units by state.
 */
export function funnelStages(funnel: InstanceFunnel, instanceType: string): { narrowing: FunnelStage[]; split: FunnelStage[] } {
  const resting: FunnelStage[] = funnel.restingUnits > 0 ? [{ label: 'resting', value: funnel.restingUnits }] : [];
  return {
    narrowing: [
      { label: instanceType === 'Radarr' ? 'movies' : 'series', value: funnel.libraryItems },
      { label: 'released', value: funnel.releasedItems },
      { label: 'monitored', value: funnel.monitoredItems },
      { label: 'tag filtered', value: funnel.tagFilteredItems },
      { label: 'need search', value: funnel.eligibleUnits },
    ],
    split: [
      { label: 'in queue', value: funnel.queuedUnits },
      { label: 'grabbed', value: funnel.recentlyGrabbedUnits },
      ...resting,
      { label: 'searched', value: funnel.searchedUnits },
      { label: 'left', value: funnel.remainingUnits },
    ],
  };
}

export const EXCLUSION_REASON_LABELS: Record<SeekerExclusionReason, string> = {
  [SeekerExclusionReason.NotReleased]: 'Not released',
  [SeekerExclusionReason.InGracePeriod]: 'In grace period',
  [SeekerExclusionReason.NotAvailable]: 'Not available in Radarr',
  [SeekerExclusionReason.NotMonitored]: 'Not monitored',
  [SeekerExclusionReason.SkipTag]: 'Skip tag',
  [SeekerExclusionReason.MissingIncludeTag]: 'No include tag',
  [SeekerExclusionReason.NothingToSearch]: 'Nothing to search',
  [SeekerExclusionReason.UpgradesDisabled]: 'Upgrades disabled',
  [SeekerExclusionReason.InQueue]: 'In queue',
  [SeekerExclusionReason.RecentlyGrabbed]: 'Recently grabbed',
  [SeekerExclusionReason.Resting]: 'Resting after repeated misses',
  [SeekerExclusionReason.SearchedThisCycle]: 'Searched this cycle',
};
