import { DestroyRef, Signal, signal } from '@angular/core';

export interface RetryCountdown {
  /** Seconds left before another attempt is allowed. Zero when no lockout is running. */
  readonly seconds: Signal<number>;
  start(seconds: number): void;
  stop(): void;
}

/**
 * Counts a rate-limit lockout down to zero, one second at a time.
 * Stops when the host component is destroyed.
 */
export function createRetryCountdown(destroyRef: DestroyRef): RetryCountdown {
  const seconds = signal(0);
  let timer: ReturnType<typeof setInterval> | null = null;

  const stop = (): void => {
    seconds.set(0);
    if (timer !== null) {
      clearInterval(timer);
      timer = null;
    }
  };

  destroyRef.onDestroy(stop);

  const start = (initial: number): void => {
    stop();
    seconds.set(initial);
    timer = setInterval(() => {
      const current = seconds();
      if (current <= 1) {
        stop();
      } else {
        seconds.set(current - 1);
      }
    }, 1000);
  };

  return { seconds: seconds.asReadonly(), start, stop };
}
