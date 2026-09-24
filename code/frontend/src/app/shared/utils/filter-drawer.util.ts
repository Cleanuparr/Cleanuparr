import { computed, Signal, signal, WritableSignal } from '@angular/core';

/** Result of {@link createFilterDrawer}. */
export interface FilterDrawer<T> {
  /** Filters currently driving the query. */
  readonly applied: Signal<T>;
  /** Filters being edited in the open drawer, discarded on close without apply. */
  readonly draft: WritableSignal<T>;
  /** Whether the drawer is open. */
  readonly drawerOpen: Signal<boolean>;
  /** Seeds the draft from the current applied filters and opens the drawer. */
  open(): void;
  /** Resets the draft back to the empty filters (drawer stays open). */
  reset(): void;
  /** Commits the draft as the applied filters and closes the drawer. */
  apply(): void;
  /** Updates a single draft field. */
  updateDraft<K extends keyof T>(key: K, value: T[K]): void;
  /** Count of applied filters that differ from the empty value. */
  readonly activeCount: Signal<number>;
}

/**
 * Applied/draft/drawer-open filter state shared by the seeker-stats tabs.
 * `activeCount` compares each field of `applied` against `empty`; pass a custom
 * comparator only if a filter's "unset" value isn't strictly equal to `empty`'s.
 */
export function createFilterDrawer<T extends Record<string, unknown>>(empty: T): FilterDrawer<T> {
  const applied = signal<T>({ ...empty });
  const draft = signal<T>({ ...empty });
  const drawerOpen = signal(false);

  const activeCount = computed(() => {
    const a = applied();
    return (Object.keys(empty) as (keyof T)[]).filter((key) => a[key] !== empty[key]).length;
  });

  const open = (): void => {
    draft.set({ ...applied() });
    drawerOpen.set(true);
  };

  const reset = (): void => {
    draft.set({ ...empty });
  };

  const apply = (): void => {
    applied.set({ ...draft() });
    drawerOpen.set(false);
  };

  const updateDraft = <K extends keyof T>(key: K, value: T[K]): void => {
    draft.update((d) => ({ ...d, [key]: value }));
  };

  return {
    applied: applied.asReadonly(),
    draft,
    drawerOpen: drawerOpen.asReadonly(),
    open,
    reset,
    apply,
    updateDraft,
    activeCount,
  };
}
