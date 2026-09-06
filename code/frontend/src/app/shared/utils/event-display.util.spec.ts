import { EventType } from '@shared/models/enums';
import {
  eventMarkerClass,
  eventSeverity,
  eventTypeSeverity,
  formatEventType,
} from './event-display.util';

describe('eventTypeSeverity', () => {
  it.each([
    [EventType.StrikeReset, 'success'],
    [EventType.DownloadCleaned, 'success'],
    [EventType.FailedImportStrike, 'error'],
    [EventType.QueueItemDeleted, 'error'],
    [EventType.StalledStrike, 'warning'],
    [EventType.DownloadMarkedForDeletion, 'warning'],
    [EventType.DownloadStopped, 'info'],
    [EventType.DownloadingMetadataStrike, 'info'],
    [EventType.SlowSpeedStrike, 'info'],
    [EventType.SlowTimeStrike, 'info'],
    [EventType.DeadTorrentStrike, 'info'],
    [EventType.CategoryChanged, 'info'],
  ])('maps %s to %s', (eventType, expected) => {
    expect(eventTypeSeverity(eventType)).toBe(expected);
  });

  it('falls back to default for unknown event types', () => {
    expect(eventTypeSeverity('SomethingThisBuildDoesNotKnow')).toBe('default');
    expect(eventTypeSeverity(EventType.SearchTriggered)).toBe('default');
  });
});

describe('eventSeverity', () => {
  it.each([
    ['Error', 'error'],
    ['error', 'error'],
    ['Warning', 'warning'],
    ['Important', 'warning'],
    ['Information', 'info'],
    ['info', 'info'],
  ])('maps %s to %s regardless of casing', (severity, expected) => {
    expect(eventSeverity(severity)).toBe(expected);
  });

  it('falls back to default for unknown severities', () => {
    expect(eventSeverity('Verbose')).toBe('default');
  });
});

describe('eventMarkerClass', () => {
  it('marks a strike reset as success', () => {
    expect(eventMarkerClass(EventType.StrikeReset, 'Information')).toBe('success');
  });

  it('keeps strikes amber unless the severity is an error', () => {
    expect(eventMarkerClass(EventType.StalledStrike, 'Information')).toBe('warning');
    expect(eventMarkerClass(EventType.StalledStrike, 'Important')).toBe('warning');
    expect(eventMarkerClass(EventType.StalledStrike, 'Error')).toBe('error');
  });

  it('falls back to the severity for non-strike events', () => {
    expect(eventMarkerClass(EventType.DownloadStopped, 'Information')).toBe('info');
    expect(eventMarkerClass(EventType.DownloadStopped, 'Error')).toBe('error');
  });
});

describe('formatEventType', () => {
  it('splits pascal case into words', () => {
    expect(formatEventType(EventType.DownloadStopped)).toBe('Download Stopped');
    expect(formatEventType(EventType.FailedImportStrike)).toBe('Failed Import Strike');
  });

  it('leaves a single word untouched', () => {
    expect(formatEventType('Stopped')).toBe('Stopped');
  });
});
