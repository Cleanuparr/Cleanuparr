import { signal } from '@angular/core';

const DEFAULT_SPINNER_DELAY_MS = 200;

export class DeferredLoader {
  readonly loading = signal(false);
  readonly showSpinner = signal(false);
  private timer: ReturnType<typeof setTimeout> | null = null;

  constructor(private readonly delayMs = DEFAULT_SPINNER_DELAY_MS) {}

  start(): void {
    this.stop();
    this.loading.set(true);
    this.timer = setTimeout(() => {
      if (this.loading()) {
        this.showSpinner.set(true);
      }
    }, this.delayMs);
  }

  stop(): void {
    if (this.timer) {
      clearTimeout(this.timer);
      this.timer = null;
    }
    this.loading.set(false);
    this.showSpinner.set(false);
  }
}
