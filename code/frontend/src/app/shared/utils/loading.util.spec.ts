import { DeferredLoader } from './loading.util';

describe('DeferredLoader', () => {
  function setup(delayMs?: number): DeferredLoader {
    return delayMs === undefined ? new DeferredLoader() : new DeferredLoader(delayMs);
  }

  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('marks itself as loading immediately without showing the spinner', () => {
    const loader = setup();

    loader.start();

    expect(loader.loading()).toBe(true);
    expect(loader.showSpinner()).toBe(false);
  });

  it('shows the spinner only once the default delay has elapsed', () => {
    const loader = setup();

    loader.start();
    vi.advanceTimersByTime(199);

    expect(loader.showSpinner()).toBe(false);

    vi.advanceTimersByTime(1);

    expect(loader.showSpinner()).toBe(true);
  });

  it('never shows the spinner when stopped before the delay elapses', () => {
    const loader = setup();

    loader.start();
    vi.advanceTimersByTime(100);
    loader.stop();
    vi.advanceTimersByTime(5000);

    expect(loader.loading()).toBe(false);
    expect(loader.showSpinner()).toBe(false);
  });

  it('restarts the delay when started again instead of leaving the first timer running', () => {
    const loader = setup();

    loader.start();
    vi.advanceTimersByTime(100);
    loader.start();
    vi.advanceTimersByTime(150);

    expect(loader.showSpinner()).toBe(false);

    vi.advanceTimersByTime(50);

    expect(loader.showSpinner()).toBe(true);
  });
});
