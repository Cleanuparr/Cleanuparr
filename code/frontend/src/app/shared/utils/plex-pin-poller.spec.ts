import { DestroyRef } from '@angular/core';
import { Observable, of, throwError } from 'rxjs';
import { pollPlexPin } from './plex-pin-poller';

interface PinResult {
  completed: boolean;
}

describe('pollPlexPin', () => {
  function setup(options: { verify?: () => Observable<PinResult>; maxAttempts?: number } = {}) {
    const destroyCallbacks: (() => void)[] = [];
    const destroyRef = {
      onDestroy: (callback: () => void) => {
        destroyCallbacks.push(callback);
        return () => undefined;
      },
    } as unknown as DestroyRef;

    const verify = vi.fn<() => Observable<PinResult>>(options.verify ?? (() => of({ completed: false })));
    const onCompleted = vi.fn<(result: PinResult) => void>();
    const onError = vi.fn<(error: unknown) => void>();
    const onTimeout = vi.fn<() => void>();

    pollPlexPin<PinResult>({
      verify,
      onCompleted,
      onError,
      onTimeout,
      destroyRef,
      intervalMs: 1000,
      maxAttempts: options.maxAttempts ?? 60,
    });

    return { destroyCallbacks, verify, onCompleted, onError, onTimeout };
  }

  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('verifies once per interval and not before the first one elapses', () => {
    const { verify } = setup();

    vi.advanceTimersByTime(999);

    expect(verify).toHaveBeenCalledTimes(0);

    vi.advanceTimersByTime(1);

    expect(verify).toHaveBeenCalledTimes(1);

    vi.advanceTimersByTime(2000);

    expect(verify).toHaveBeenCalledTimes(3);
  });

  it('reports completion once and stops polling', () => {
    const { verify, onCompleted } = setup({ verify: () => of({ completed: true }) });

    vi.advanceTimersByTime(1000);

    expect(onCompleted).toHaveBeenCalledTimes(1);
    expect(onCompleted).toHaveBeenCalledWith({ completed: true });

    vi.advanceTimersByTime(10000);

    expect(verify).toHaveBeenCalledTimes(1);
    expect(onCompleted).toHaveBeenCalledTimes(1);
  });

  it('reports a verify failure and stops polling', () => {
    const failure = new Error('plex is down');
    const { verify, onError } = setup({ verify: () => throwError(() => failure) });

    vi.advanceTimersByTime(1000);

    expect(onError).toHaveBeenCalledTimes(1);
    expect(onError).toHaveBeenCalledWith(failure);

    vi.advanceTimersByTime(10000);

    expect(verify).toHaveBeenCalledTimes(1);
    expect(onError).toHaveBeenCalledTimes(1);
  });

  it('times out once after the attempt limit is exceeded', () => {
    const { verify, onTimeout } = setup({ maxAttempts: 2 });

    vi.advanceTimersByTime(2000);

    expect(verify).toHaveBeenCalledTimes(2);
    expect(onTimeout).toHaveBeenCalledTimes(0);

    vi.advanceTimersByTime(1000);

    expect(onTimeout).toHaveBeenCalledTimes(1);

    vi.advanceTimersByTime(10000);

    expect(verify).toHaveBeenCalledTimes(2);
    expect(onTimeout).toHaveBeenCalledTimes(1);
  });

  it('stops polling when the host is destroyed', () => {
    const { destroyCallbacks, verify } = setup();

    vi.advanceTimersByTime(2000);

    expect(verify).toHaveBeenCalledTimes(2);

    for (const callback of destroyCallbacks) {
      callback();
    }
    vi.advanceTimersByTime(10000);

    expect(verify).toHaveBeenCalledTimes(2);
  });
});
