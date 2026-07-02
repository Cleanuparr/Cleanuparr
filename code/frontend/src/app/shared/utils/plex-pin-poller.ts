import { DestroyRef } from '@angular/core';
import { Observable, Subscription } from 'rxjs';

export interface PlexPinPollOptions<T extends { completed: boolean }> {
  /** Called on each attempt to check whether the Plex PIN has been authorized. */
  verify: () => Observable<T>;
  onCompleted: (result: T) => void;
  onError: (error: unknown) => void;
  onTimeout: () => void;
  destroyRef: DestroyRef;
  intervalMs?: number;
  maxAttempts?: number;
}

/**
 * Polls a Plex PIN verify endpoint until it completes, errors, or times out.
 * Owns its interval and any in-flight verify request, and stops automatically
 * when the host component is destroyed.
 */
export function pollPlexPin<T extends { completed: boolean }>(options: PlexPinPollOptions<T>): void {
  const { verify, onCompleted, onError, onTimeout, destroyRef } = options;
  const intervalMs = options.intervalMs ?? 2000;
  const maxAttempts = options.maxAttempts ?? 60;

  let attempts = 0;
  let inFlight: Subscription | null = null;
  let timer: ReturnType<typeof setInterval> | null = null;

  const stop = (): void => {
    if (timer !== null) {
      clearInterval(timer);
      timer = null;
    }
    inFlight?.unsubscribe();
    inFlight = null;
  };

  destroyRef.onDestroy(stop);

  timer = setInterval(() => {
    attempts++;
    if (attempts > maxAttempts) {
      stop();
      onTimeout();
      return;
    }

    inFlight = verify().subscribe({
      next: (result) => {
        if (result.completed) {
          stop();
          onCompleted(result);
        }
      },
      error: (error) => {
        stop();
        onError(error);
      },
    });
  }, intervalMs);
}
