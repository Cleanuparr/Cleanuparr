import { JobStatus, JobType } from '@shared/models/enums';
import { jobDisplayName, jobStatusSeverity } from './job-display.util';

describe('jobDisplayName', () => {
  it.each([
    [JobType.QueueCleaner, 'Queue Cleaner'],
    [JobType.MalwareBlocker, 'Malware Blocker'],
    [JobType.DownloadCleaner, 'Download Cleaner'],
    [JobType.BlacklistSynchronizer, 'Blacklist Sync'],
  ])('names %s as %s', (jobType, expected) => {
    expect(jobDisplayName(jobType)).toBe(expected);
  });

  it('returns unmapped types unchanged', () => {
    expect(jobDisplayName(JobType.Seeker)).toBe('Seeker');
    expect(jobDisplayName('SomethingElse')).toBe('SomethingElse');
  });
});

describe('jobStatusSeverity', () => {
  it.each([
    [JobStatus.Running, 'info'],
    [JobStatus.Complete, 'success'],
    [JobStatus.Scheduled, 'success'],
    [JobStatus.Error, 'error'],
    [JobStatus.Paused, 'warning'],
    [JobStatus.NotScheduled, 'default'],
    [JobStatus.NotFound, 'default'],
    [JobStatus.Unknown, 'default'],
  ])('maps %s to %s', (status, expected) => {
    expect(jobStatusSeverity(status)).toBe(expected);
  });

  it('matches regardless of casing', () => {
    expect(jobStatusSeverity('running')).toBe('info');
  });

  it('falls back to default for an unknown status', () => {
    expect(jobStatusSeverity('Whatever')).toBe('default');
  });
});
