import { DestroyRef } from '@angular/core';
import { createRetryCountdown, RetryCountdown } from './retry-countdown';

describe('createRetryCountdown', () => {
  function setup(): { countdown: RetryCountdown; destroy: () => void } {
    let onDestroy: (() => void) | null = null;
    const destroyRef = { onDestroy: (callback: () => void) => (onDestroy = callback) } as unknown as DestroyRef;
    const countdown = createRetryCountdown(destroyRef);

    return { countdown, destroy: () => onDestroy!() };
  }

  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('starts at zero', () => {
    const { countdown } = setup();

    expect(countdown.seconds()).toBe(0);
  });

  it('ticks down once per second and stops at zero', () => {
    const { countdown } = setup();

    countdown.start(3);

    expect(countdown.seconds()).toBe(3);

    vi.advanceTimersByTime(2000);

    expect(countdown.seconds()).toBe(1);

    vi.advanceTimersByTime(1000);

    expect(countdown.seconds()).toBe(0);

    vi.advanceTimersByTime(5000);

    expect(countdown.seconds()).toBe(0);
  });

  it('restarts from the latest value', () => {
    const { countdown } = setup();

    countdown.start(3);
    vi.advanceTimersByTime(1000);
    countdown.start(10);

    expect(countdown.seconds()).toBe(10);

    vi.advanceTimersByTime(1000);

    expect(countdown.seconds()).toBe(9);
  });

  it('clears the timer when the host is destroyed', () => {
    const { countdown, destroy } = setup();

    countdown.start(5);
    destroy();

    expect(countdown.seconds()).toBe(0);
    expect(vi.getTimerCount()).toBe(0);
  });
});
