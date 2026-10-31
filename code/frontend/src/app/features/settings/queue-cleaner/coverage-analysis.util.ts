import { QueueRule } from '@shared/models/queue-rule.model';
import { QueueRuleContentType, TorrentPrivacyType } from '@shared/models/enums';

export interface CoverageGap {
  privacyType: string;
  contentType: string;
  from: number;
  to: number;
}

export interface CoverageResult {
  hasGaps: boolean;
  gaps: CoverageGap[];
}

const PRIVACY_TYPES = [TorrentPrivacyType.Public, TorrentPrivacyType.Private];
const CONTENT_TYPES = [QueueRuleContentType.New, QueueRuleContentType.Upgrade];

export function analyzeCoverage(rules: QueueRule[]): CoverageResult {
  const enabledRules = rules.filter((r) => r.enabled);
  const gaps: CoverageGap[] = [];

  for (const privacyType of PRIVACY_TYPES) {
    for (const contentType of CONTENT_TYPES) {
      const intervals = enabledRules
        .filter((r) =>
          (r.privacyType === privacyType || r.privacyType === TorrentPrivacyType.Both) &&
          (r.contentType === contentType || r.contentType === QueueRuleContentType.All))
        .map((r) => ({
          start: Math.max(0, Math.min(100, r.minCompletionPercentage)),
          end: Math.max(0, Math.min(100, r.maxCompletionPercentage)),
        }))
        .filter((i) => i.end >= i.start)
        .sort((a, b) => a.start === b.start ? a.end - b.end : a.start - b.start);

      if (intervals.length === 0) {
        gaps.push({ privacyType, contentType, from: 0, to: 100 });
        continue;
      }

      let cursor = 0;

      for (const interval of intervals) {
        if (interval.start > cursor) {
          gaps.push({ privacyType, contentType, from: cursor, to: interval.start });
        }
        if (interval.end > cursor) {
          cursor = interval.end;
        }
        if (cursor >= 100) {
          cursor = 100;
          break;
        }
      }

      if (cursor < 100) {
        gaps.push({ privacyType, contentType, from: cursor, to: 100 });
      }
    }
  }

  return { hasGaps: gaps.length > 0, gaps };
}
