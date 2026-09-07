import { EventSeverity, JobStatus } from '@shared/models/enums';
import { matchEnum } from './enum-match.util';

describe('matchEnum', () => {
  it('matches an exact enum value', () => {
    expect(matchEnum(EventSeverity, 'Important')).toBe(EventSeverity.Important);
  });

  it('matches regardless of casing', () => {
    expect(matchEnum(EventSeverity, 'error')).toBe(EventSeverity.Error);
    expect(matchEnum(EventSeverity, 'INFORMATION')).toBe(EventSeverity.Information);
  });

  it('matches values that contain a space', () => {
    expect(matchEnum(JobStatus, 'not scheduled')).toBe(JobStatus.NotScheduled);
  });

  it('returns null for an unknown value', () => {
    expect(matchEnum(EventSeverity, 'Verbose')).toBeNull();
  });

  it('returns null for empty input', () => {
    expect(matchEnum(EventSeverity, '')).toBeNull();
    expect(matchEnum(EventSeverity, null)).toBeNull();
    expect(matchEnum(EventSeverity, undefined)).toBeNull();
  });
});
