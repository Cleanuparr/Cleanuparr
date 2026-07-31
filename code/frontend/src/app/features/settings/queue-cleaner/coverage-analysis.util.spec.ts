import { TorrentPrivacyType } from '@shared/models/enums';
import { QueueRule } from '@shared/models/queue-rule.model';
import { analyzeCoverage } from './coverage-analysis.util';

function rule(overrides: Partial<QueueRule>): QueueRule {
  return {
    name: 'rule',
    enabled: true,
    maxStrikes: 3,
    privacyType: TorrentPrivacyType.Both,
    minCompletionPercentage: 0,
    maxCompletionPercentage: 100,
    deletePrivateTorrentsFromClient: false,
    changeCategory: false,
    ...overrides,
  };
}

describe('analyzeCoverage', () => {
  it('reports a full gap for both privacy types when there are no rules', () => {
    expect(analyzeCoverage([])).toEqual({
      hasGaps: true,
      gaps: [
        { privacyType: TorrentPrivacyType.Public, from: 0, to: 100 },
        { privacyType: TorrentPrivacyType.Private, from: 0, to: 100 },
      ],
    });
  });

  it('ignores disabled rules even when they would close the gap', () => {
    const result = analyzeCoverage([rule({ enabled: false })]);

    expect(result.hasGaps).toBe(true);
    expect(result.gaps).toHaveLength(2);
  });

  it('counts a Both rule toward Public and Private', () => {
    expect(analyzeCoverage([rule({ privacyType: TorrentPrivacyType.Both })])).toEqual({
      hasGaps: false,
      gaps: [],
    });
  });

  it('leaves the other privacy type uncovered when a rule targets only one', () => {
    const result = analyzeCoverage([rule({ privacyType: TorrentPrivacyType.Public })]);

    expect(result.gaps).toEqual([
      { privacyType: TorrentPrivacyType.Private, from: 0, to: 100 },
    ]);
  });

  it('reports leading, interior and trailing gaps', () => {
    const leading = analyzeCoverage([rule({ minCompletionPercentage: 20 })]);
    expect(leading.gaps).toContainEqual({
      privacyType: TorrentPrivacyType.Public,
      from: 0,
      to: 20,
    });

    const trailing = analyzeCoverage([rule({ maxCompletionPercentage: 80 })]);
    expect(trailing.gaps).toContainEqual({
      privacyType: TorrentPrivacyType.Public,
      from: 80,
      to: 100,
    });

    const interior = analyzeCoverage([
      rule({ minCompletionPercentage: 0, maxCompletionPercentage: 40 }),
      rule({ minCompletionPercentage: 60, maxCompletionPercentage: 100 }),
    ]);
    expect(interior.gaps).toContainEqual({
      privacyType: TorrentPrivacyType.Public,
      from: 40,
      to: 60,
    });
  });

  it('merges overlapping and nested intervals without regressing the cursor', () => {
    const result = analyzeCoverage([
      rule({ minCompletionPercentage: 0, maxCompletionPercentage: 50 }),
      rule({ minCompletionPercentage: 10, maxCompletionPercentage: 30 }),
      rule({ minCompletionPercentage: 40, maxCompletionPercentage: 100 }),
    ]);

    expect(result).toEqual({ hasGaps: false, gaps: [] });
  });

  it('clamps out-of-range percentages instead of rejecting the rule', () => {
    const result = analyzeCoverage([
      rule({ minCompletionPercentage: -10, maxCompletionPercentage: 130 }),
    ]);

    expect(result).toEqual({ hasGaps: false, gaps: [] });
  });

  it('drops inverted intervals', () => {
    const result = analyzeCoverage([
      rule({ minCompletionPercentage: 80, maxCompletionPercentage: 20 }),
    ]);

    expect(result.gaps).toContainEqual({
      privacyType: TorrentPrivacyType.Public,
      from: 0,
      to: 100,
    });
  });

  it('orders equal starts by end so the widest interval wins', () => {
    const result = analyzeCoverage([
      rule({ minCompletionPercentage: 0, maxCompletionPercentage: 100 }),
      rule({ minCompletionPercentage: 0, maxCompletionPercentage: 10 }),
    ]);

    expect(result).toEqual({ hasGaps: false, gaps: [] });
  });
});
