import { SeekerSearchReason, SeekerSearchType } from '@core/models/search-stats.models';
import type { InstanceFunnel, InstanceSearchStat } from '@core/models/search-stats.models';
import {
  cycleProgress,
  formatCycleDuration,
  formatDurationBetween,
  formatGrabbedItems,
  formatSearchReason,
  funnelStages,
  instanceHealthWarning,
  searchReasonSeverity,
  searchStatusSeverity,
  searchTypeSeverity,
} from './search-display.util';

function instance(partial: Partial<InstanceSearchStat> = {}): InstanceSearchStat {
  return {
    instanceId: 'instance-1',
    instanceName: 'Instance',
    instanceType: 'Sonarr',
    instanceVersion: 0,
    itemsTracked: 10,
    totalSearchCount: 5,
    lastSearchedAt: '2026-07-30T10:00:00Z',
    lastProcessedAt: '2026-07-30T10:00:00Z',
    currentCycleId: 'cycle-1',
    cycleItemsSearched: 3,
    cycleItemsTotal: 8,
    cycleStartedAt: '2026-07-29T09:00:00Z',
    cycleItemsRemaining: 5,
    cycleCompletedAt: null,
    lastCycleStartedAt: null,
    lastCycleCompletedAt: null,
    funnel: null,
    ...partial,
  };
}

describe('formatSearchReason', () => {
  it.each([
    [SeekerSearchReason.Missing, 'Missing'],
    [SeekerSearchReason.QualityCutoffNotMet, 'Cutoff Unmet'],
    [SeekerSearchReason.CustomFormatScoreBelowCutoff, 'CF Below Cutoff'],
    [SeekerSearchReason.Replacement, 'Replacement'],
  ])('labels %s as %s', (reason, expected) => {
    expect(formatSearchReason(reason)).toBe(expected);
  });

  it('passes an unknown reason through unchanged', () => {
    expect(formatSearchReason('Whatever')).toBe('Whatever');
  });
});

describe('searchReasonSeverity', () => {
  it.each([
    [SeekerSearchReason.Missing, 'error'],
    [SeekerSearchReason.QualityCutoffNotMet, 'warning'],
    [SeekerSearchReason.CustomFormatScoreBelowCutoff, 'warning'],
    [SeekerSearchReason.Replacement, 'info'],
  ])('maps %s to %s', (reason, expected) => {
    expect(searchReasonSeverity(reason)).toBe(expected);
  });

  it('falls back to default for an unknown reason', () => {
    expect(searchReasonSeverity('Whatever')).toBe('default');
  });
});

describe('searchTypeSeverity', () => {
  it('flags a replacement search as warning and everything else as info', () => {
    expect(searchTypeSeverity(SeekerSearchType.Replacement)).toBe('warning');
    expect(searchTypeSeverity(SeekerSearchType.Proactive)).toBe('info');
  });
});

describe('searchStatusSeverity', () => {
  it.each([
    ['Completed', 'success'],
    ['Failed', 'error'],
    ['TimedOut', 'warning'],
    ['Started', 'info'],
    ['Pending', 'default'],
  ])('maps %s to %s', (status, expected) => {
    expect(searchStatusSeverity(status)).toBe(expected);
  });
});

describe('formatGrabbedItems', () => {
  it('joins the release names with a comma', () => {
    expect(formatGrabbedItems(['Release.One', 'Release.Two'])).toBe('Release.One, Release.Two');
  });

  it('returns an empty string for no releases', () => {
    expect(formatGrabbedItems([])).toBe('');
  });
});

describe('cycleProgress', () => {
  it('rounds the searched share of the cycle', () => {
    expect(cycleProgress(instance({ cycleItemsSearched: 3, cycleItemsTotal: 8 }))).toBe(38);
  });

  it('caps at 100 when more items were searched than tracked', () => {
    expect(cycleProgress(instance({ cycleItemsSearched: 12, cycleItemsTotal: 8 }))).toBe(100);
  });

  it('returns 0 when the cycle has no items', () => {
    expect(cycleProgress(instance({ cycleItemsTotal: 0 }))).toBe(0);
  });
});

describe('instanceHealthWarning', () => {
  it('warns when an instance has never searched', () => {
    expect(instanceHealthWarning(instance({ lastSearchedAt: null, totalSearchCount: 0 }))).toBe('Never searched');
  });

  it('returns null once an instance has searched', () => {
    expect(instanceHealthWarning(instance())).toBeNull();
  });
});

describe('formatCycleDuration', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-07-31T12:00:00Z'));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('formats down to the largest non-zero unit', () => {
    expect(formatCycleDuration('2026-07-29T09:00:00Z')).toBe('2d 3h');
    expect(formatCycleDuration('2026-07-31T07:00:00Z')).toBe('5h');
    expect(formatCycleDuration('2026-07-31T11:30:00Z')).toBe('30m');
  });
});

describe('formatDurationBetween', () => {
  it('formats the span between two fixed timestamps', () => {
    expect(formatDurationBetween('2026-07-29T09:00:00Z', '2026-07-31T12:00:00Z')).toBe('2d 3h');
    expect(formatDurationBetween('2026-07-31T07:00:00Z', '2026-07-31T12:00:00Z')).toBe('5h');
    expect(formatDurationBetween('2026-07-31T11:30:00Z', '2026-07-31T12:00:00Z')).toBe('30m');
  });
});

describe('funnelStages', () => {
  const funnel: InstanceFunnel = {
    libraryItems: 10,
    releasedItems: 9,
    monitoredItems: 8,
    tagFilteredItems: 7,
    eligibleUnits: 6,
    queuedUnits: 1,
    recentlyGrabbedUnits: 2,
    restingUnits: 0,
    searchedUnits: 1,
    remainingUnits: 2,
    countedAt: '2026-07-30T10:00:00Z',
  };

  it('names the library stage after the instance type', () => {
    expect(funnelStages(funnel, 'Radarr', 0).narrowing[0]).toEqual({ label: 'movies', value: 10 });
    expect(funnelStages(funnel, 'Sonarr', 0).narrowing[0]).toEqual({ label: 'series', value: 10 });
    expect(funnelStages(funnel, 'Whisparr', 2).narrowing[0]).toEqual({ label: 'sites', value: 10 });
    expect(funnelStages(funnel, 'Whisparr', 3).narrowing[0]).toEqual({ label: 'movies', value: 10 });
    expect(funnelStages(funnel, 'Lidarr', 0).narrowing[0]).toEqual({ label: 'artists', value: 10 });
    expect(funnelStages(funnel, 'Chaptarr', 0).narrowing[0]).toEqual({ label: 'authors', value: 10 });
  });

  it('splits the units needing a search by state', () => {
    expect(funnelStages(funnel, 'Sonarr', 0).split.map((s) => s.value)).toEqual([1, 2, 1, 2]);
  });

  it('adds the resting stage only when units are resting', () => {
    expect(funnelStages(funnel, 'Sonarr', 0).split.map((s) => s.label)).not.toContain('resting');

    const split = funnelStages({ ...funnel, restingUnits: 4 }, 'Sonarr', 0).split;

    expect(split.map((s) => s.label)).toEqual(['in queue', 'grabbed', 'resting', 'searched', 'left']);
    expect(split[2].value).toBe(4);
  });
});
