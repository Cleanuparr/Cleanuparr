import { ManualEvent } from '@core/models/event.models';

export function mergeUnresolvedManualEvents(
  hubEvents: readonly ManualEvent[],
  loadedEvents: readonly ManualEvent[],
): ManualEvent[] {
  const byId = new Map<string, ManualEvent>();

  for (const event of hubEvents) {
    if (event.isResolved) {
      byId.delete(event.id);
    } else {
      byId.set(event.id, event);
    }
  }

  for (const event of loadedEvents) {
    if (event.isResolved) {
      byId.delete(event.id);
    } else {
      byId.set(event.id, event);
    }
  }

  return [...byId.values()].sort((a, b) => {
    if (a.timestamp < b.timestamp) {
      return 1;
    }
    if (a.timestamp > b.timestamp) {
      return -1;
    }
    return 0;
  });
}
