import type { BadgeSeverity } from '@ui/badge/badge.component';
import { EventSeverity, EventType } from '@shared/models/enums';
import { formatEnumValue, matchEnum } from './enum.util';

const STRIKE_EVENT_TYPES: ReadonlySet<EventType> = new Set([
  EventType.FailedImportStrike,
  EventType.StalledStrike,
  EventType.DownloadingMetadataStrike,
  EventType.SlowSpeedStrike,
  EventType.SlowTimeStrike,
  EventType.DeadTorrentStrike,
]);

export function eventTypeSeverity(eventType: string): BadgeSeverity {
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

export function eventSeverity(severity: string): BadgeSeverity {
  switch (matchEnum(EventSeverity, severity) ?? aliasedSeverity(severity)) {
    case EventSeverity.Error:
      return 'error';
    case EventSeverity.Warning:
    case EventSeverity.Important:
      return 'warning';
    case EventSeverity.Information:
      return 'info';
    default:
      return 'default';
  }
}

export function eventMarkerClass(eventType: string, severity: string): string {
  if (matchEnum(EventType, eventType) === EventType.StrikeReset) {
    return 'success';
  }
  if (isStrike(eventType)) {
    return eventSeverity(severity) === 'error' ? 'error' : 'warning';
  }
  return eventSeverity(severity);
}

export function eventIcon(eventType: string): string {
  const type = matchEnum(EventType, eventType);
  if (type === EventType.StrikeReset) {
    return 'tablerHistory';
  }
  if (isStrike(eventType)) {
    return 'tablerBolt';
  }
  switch (type) {
    case EventType.DownloadCleaned:
      return 'tablerDownload';
    case EventType.QueueItemDeleted:
      return 'tablerTrash';
    case EventType.CategoryChanged:
      return 'tablerTag';
    default:
      return 'tablerCircle';
  }
}

export function manualEventSeverityClass(severity: string): string {
  switch (matchEnum(EventSeverity, severity)) {
    case EventSeverity.Error:
      return 'manual-event--error';
    case EventSeverity.Warning:
      return 'manual-event--warning';
    case EventSeverity.Important:
      return 'manual-event--important';
    default:
      return 'manual-event--info';
  }
}

export function formatEventType(eventType: string): string {
  return formatEnumValue(eventType);
}

function isStrike(eventType: string): boolean {
  const type = matchEnum(EventType, eventType);
  if (type !== null) {
    return STRIKE_EVENT_TYPES.has(type);
  }
  // A type this build does not know still renders as a strike when it reads like one.
  return eventType.toLowerCase().includes('strike');
}

// The API spells it "Information"; "info" is accepted as well.
function aliasedSeverity(severity: string): EventSeverity | null {
  return severity.toLowerCase() === 'info' ? EventSeverity.Information : null;
}
