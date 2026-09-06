import { EventType } from '@shared/models/enums';

export type EventSeverityTone = 'error' | 'warning' | 'info' | 'default';

export type EventTypeTone = EventSeverityTone | 'success';

export function eventTypeSeverity(eventType: string): EventTypeTone {
  switch (eventType) {
    case EventType.StrikeReset:
    case EventType.DownloadCleaned:
      return 'success';
    case EventType.FailedImportStrike:
    case EventType.QueueItemDeleted:
      return 'error';
    case EventType.StalledStrike:
    case EventType.DownloadMarkedForDeletion:
      return 'warning';
    case EventType.DownloadStopped:
    case EventType.DownloadingMetadataStrike:
    case EventType.SlowSpeedStrike:
    case EventType.SlowTimeStrike:
    case EventType.DeadTorrentStrike:
    case EventType.CategoryChanged:
      return 'info';
    default:
      return 'default';
  }
}

export function eventSeverity(severity: string): EventSeverityTone {
  const s = severity.toLowerCase();
  if (s === 'error') {
    return 'error';
  }
  if (s === 'warning' || s === 'important') {
    return 'warning';
  }
  if (s === 'information' || s === 'info') {
    return 'info';
  }
  return 'default';
}

export function eventMarkerClass(eventType: string, severity: string): string {
  const t = eventType.toLowerCase();
  if (t === 'strikereset') {
    return 'success';
  }
  if (t.includes('strike')) {
    return eventSeverity(severity) === 'error' ? 'error' : 'warning';
  }
  return eventSeverity(severity);
}

export function formatEventType(eventType: string): string {
  return eventType.replace(/([A-Z])/g, ' $1').trim();
}
