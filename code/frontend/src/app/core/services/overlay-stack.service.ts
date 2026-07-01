import { Injectable, signal } from '@angular/core';

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
