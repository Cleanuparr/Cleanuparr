import { computed, Signal, signal } from '@angular/core';

/** Result of {@link createDirtyTracker}. */
export interface DirtyTracker {
  /** JSON snapshot of the model as of the last `markSaved()` call, or `''` before the first one. */
  readonly snapshot: Signal<string>;
  /** `true` once the model's current JSON differs from the last saved snapshot. */
  readonly dirty: Signal<boolean>;
  /** Captures the model's current value as the new saved snapshot (call after load and after save). */
  markSaved(): void;
}

/**
 * JSON-snapshot-comparison dirty tracking, shared by every settings component.
 * Signal Forms' own `dirty()` means "touched", not "differs from saved" — this is what
 * `hasPendingChanges()` guards must use instead.
 */
export function createDirtyTracker<T>(
  model: Signal<T>,
  toSnapshot: (value: T) => unknown = (value) => value,
): DirtyTracker {
  const snapshot = signal('');

  const dirty = computed(() => {
    const saved = snapshot();
    return saved !== '' && saved !== JSON.stringify(toSnapshot(model()));
  });

  const markSaved = (): void => {
    snapshot.set(JSON.stringify(toSnapshot(model())));
  };

  return { snapshot: snapshot.asReadonly(), dirty, markSaved };
}
