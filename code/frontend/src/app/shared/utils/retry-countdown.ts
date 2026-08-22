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

    // Tracked as an expiry so a throttled or suspended timer cannot outlive the lockout
    const expiresAt = Date.now() + initial * 1000;
    seconds.set(initial);

    timer = setInterval(() => {
      const remaining = Math.ceil((expiresAt - Date.now()) / 1000);
      if (remaining <= 0) {
        stop();
      } else {
        seconds.set(remaining);
      }
    }, 1000);
  };

  return { seconds: seconds.asReadonly(), start, stop };
}
