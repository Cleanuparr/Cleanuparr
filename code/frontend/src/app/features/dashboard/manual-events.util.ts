import { ManualEvent } from '@core/models/event.models';

export function mergeUnresolvedManualEvents(
  hubEvents: readonly ManualEvent[],
  loadedEvents: readonly ManualEvent[],
): ManualEvent[] {
  const resolved = new Set<string>();
  for (const event of [...hubEvents, ...loadedEvents]) {
    if (event.isResolved) {
      resolved.add(event.id);
    }
  }

  const byId = new Map<string, ManualEvent>();
  for (const event of [...hubEvents, ...loadedEvents]) {
    if (!resolved.has(event.id)) {
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
