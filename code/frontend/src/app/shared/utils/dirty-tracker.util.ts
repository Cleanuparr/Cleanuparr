import { computed, Signal, signal } from '@angular/core';

/** How long the "saved" flash stays true after a successful save, shared by every settings page. */
export const SAVED_FLASH_MS = 1500;

/** Result of {@link createDirtyTracker}. */
export interface DirtyTracker<T> {
  /** JSON snapshot of the model as of the last `markSaved()` call, or `''` before the first one. */
  readonly snapshot: Signal<string>;
  /** `true` once the model's current JSON differs from the last saved snapshot. */
  readonly dirty: Signal<boolean>;
  /** Captures `value`, or the model's current value, as the new saved snapshot. */
  markSaved(value?: T): void;
}

/**
 * JSON-snapshot-comparison dirty tracking, shared by every settings component.
 * Signal Forms' own `dirty()` means "touched", not "differs from saved",
 * so `hasPendingChanges()` guards must use this instead.
 */
export function createDirtyTracker<T>(
  model: Signal<T>,
  toSnapshot: (value: T) => unknown = (value) => value,
): DirtyTracker<T> {
  const snapshot = signal('');

  const dirty = computed(() => {
    const saved = snapshot();
    return saved !== '' && saved !== JSON.stringify(toSnapshot(model()));
  });

  const markSaved = (value?: T): void => {
    snapshot.set(JSON.stringify(toSnapshot(value ?? model())));
  };

  return { snapshot: snapshot.asReadonly(), dirty, markSaved };
}
