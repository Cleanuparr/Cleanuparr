import { TestBed } from '@angular/core/testing';
import { ToastService } from './toast.service';

describe('ToastService', () => {
  function setup(): ToastService {
    return TestBed.inject(ToastService);
  }

  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('appends a toast and reports that toasts are present', () => {
    const toasts = setup();

    expect(toasts.hasToasts()).toBe(false);

    toasts.success('Saved');

    expect(toasts.hasToasts()).toBe(true);
    expect(toasts.toasts()).toEqual([{ id: 0, severity: 'success', message: 'Saved', duration: 4000 }]);
  });

  it('drops a repeated message inside the dedup window but keeps it afterwards', () => {
    const toasts = setup();

    toasts.info('Still working');
    vi.advanceTimersByTime(299);
    toasts.info('Still working');

    expect(toasts.toasts().length).toBe(1);

    vi.advanceTimersByTime(1);
    toasts.info('Still working');

    expect(toasts.toasts().length).toBe(2);
  });

  it('keeps two different messages raised inside the dedup window', () => {
    const toasts = setup();

    toasts.info('First');
    vi.advanceTimersByTime(10);
    toasts.info('Second');

    expect(toasts.toasts().map((toast) => toast.message)).toEqual(['First', 'Second']);
  });

  it('auto-dismisses each toast when its own duration elapses', () => {
    const toasts = setup();

    toasts.success('Saved');
    toasts.error('Failed');

    vi.advanceTimersByTime(4000);

    expect(toasts.toasts().map((toast) => toast.message)).toEqual(['Failed']);

    vi.advanceTimersByTime(2000);

    expect(toasts.toasts()).toEqual([]);
    expect(toasts.hasToasts()).toBe(false);
  });
});
