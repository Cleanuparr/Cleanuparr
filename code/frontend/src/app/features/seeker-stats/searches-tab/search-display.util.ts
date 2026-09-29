import type { BadgeSeverity } from '@ui/badge/badge.component';
import { SeekerSearchType, SeekerSearchReason, SearchCommandStatus } from '@core/models/search-stats.models';
import type { InstanceSearchStat } from '@core/models/search-stats.models';

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
  const start = new Date(cycleStartedAt);
  const now = new Date();
  const diffMs = now.getTime() - start.getTime();
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
