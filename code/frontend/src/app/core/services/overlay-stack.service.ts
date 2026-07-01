import { Injectable, signal, effect, inject, DestroyRef, Signal } from '@angular/core';

/**
 * Tracks the stack of currently-open overlays (modals, drawers, confirm dialog,
 * mobile menu) in open order, so a single Escape press dismisses only the
 * top-most overlay instead of every open one closing at once.
 */
@Injectable({ providedIn: 'root' })
export class OverlayStackService {
  private readonly stack = signal<number[]>([]);
  private counter = 0;

  register(): number {
    const id = ++this.counter;
    this.stack.update((s) => [...s, id]);
    return id;
  }

  unregister(id: number): void {
    this.stack.update((s) => s.filter((x) => x !== id));
  }

  isTopmost(id: number): boolean {
    const s = this.stack();
    return s.length > 0 && s[s.length - 1] === id;
  }
}

/**
 * Registers an overlay in the shared stack while `isOpen` is true and unregisters
 * it when it closes or the host is destroyed. Returns a predicate that reports
 * whether this overlay is currently top-most (for Escape handling).
 * Must be called in an injection context.
 */
export function registerOverlayEffect(isOpen: Signal<unknown>): () => boolean {
  const overlays = inject(OverlayStackService);
  let overlayId: number | null = null;
  effect(() => {
    if (isOpen()) {
      overlayId ??= overlays.register();
    } else if (overlayId !== null) {
      overlays.unregister(overlayId);
      overlayId = null;
    }
  });
  inject(DestroyRef).onDestroy(() => {
    if (overlayId !== null) {
      overlays.unregister(overlayId);
    }
  });
  return () => overlayId !== null && overlays.isTopmost(overlayId);
}
