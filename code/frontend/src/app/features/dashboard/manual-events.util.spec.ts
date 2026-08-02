import { ManualEvent } from '@core/models/event.models';
import { mergeUnresolvedManualEvents } from './manual-events.util';

function event(overrides: Partial<ManualEvent> & { id: string }): ManualEvent {
  return {
    timestamp: new Date('2026-01-01T00:00:00Z'),
    message: 'message',
    severity: 'warning',
    isResolved: false,
    isDryRun: false,
    ...overrides,
  };
}

describe('mergeUnresolvedManualEvents', () => {
  it('sorts the merged list newest first', () => {
    const merged = mergeUnresolvedManualEvents(
      [event({ id: 'old', timestamp: new Date('2026-01-01T00:00:00Z') })],
      [
        event({ id: 'newest', timestamp: new Date('2026-01-03T00:00:00Z') }),
        event({ id: 'middle', timestamp: new Date('2026-01-02T00:00:00Z') }),
      ],
    );

    expect(merged.map((e) => e.id)).toEqual(['newest', 'middle', 'old']);
  });

  it('drops resolved events from both sources', () => {
    const merged = mergeUnresolvedManualEvents(
      [event({ id: 'hub-resolved', isResolved: true })],
      [event({ id: 'loaded-resolved', isResolved: true }), event({ id: 'kept' })],
    );

    expect(merged.map((e) => e.id)).toEqual(['kept']);
  });

  it('lets the loaded backlog win on an id collision', () => {
    const merged = mergeUnresolvedManualEvents(
      [event({ id: 'same', message: 'from hub' })],
      [event({ id: 'same', message: 'from backlog' })],
    );

    expect(merged).toHaveLength(1);
    expect(merged[0].message).toBe('from backlog');
  });

  it('drops an event that either source reports as resolved, whichever side is stale', () => {
    const unresolved = event({ id: 'same' });
    const resolved = event({ id: 'same', isResolved: true });

    expect(mergeUnresolvedManualEvents([unresolved], [resolved])).toEqual([]);
    expect(mergeUnresolvedManualEvents([resolved], [unresolved])).toEqual([]);
    expect(mergeUnresolvedManualEvents([resolved], [resolved])).toEqual([]);
  });

  it('drops a resolved event without hiding the unresolved ones alongside it', () => {
    const merged = mergeUnresolvedManualEvents(
      [event({ id: 'resolved-later', isResolved: true }), event({ id: 'kept-hub' })],
      [event({ id: 'resolved-later' }), event({ id: 'kept-backlog' })],
    );

    expect(merged.map((e) => e.id).sort()).toEqual(['kept-backlog', 'kept-hub']);
  });

  it('returns an empty list when there is nothing unresolved', () => {
    expect(mergeUnresolvedManualEvents([], [])).toEqual([]);
    expect(mergeUnresolvedManualEvents([event({ id: 'a', isResolved: true })], [])).toEqual([]);
  });
});
