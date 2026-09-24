import { signal } from '@angular/core';
import { createDirtyTracker } from './dirty-tracker.util';

describe('createDirtyTracker', () => {
  it('is not dirty before the first markSaved() call', () => {
    const model = signal({ enabled: true });
    const tracker = createDirtyTracker(model);

    expect(tracker.snapshot()).toBe('');
    expect(tracker.dirty()).toBe(false);
  });

  it('becomes dirty once the model diverges from the saved snapshot', () => {
    const model = signal({ enabled: true });
    const tracker = createDirtyTracker(model);

    tracker.markSaved();
    expect(tracker.dirty()).toBe(false);

    model.set({ enabled: false });
    expect(tracker.dirty()).toBe(true);
  });

  it('clears dirty again once markSaved() is called after an edit', () => {
    const model = signal({ enabled: true });
    const tracker = createDirtyTracker(model);

    tracker.markSaved();
    model.set({ enabled: false });
    expect(tracker.dirty()).toBe(true);

    tracker.markSaved();
    expect(tracker.dirty()).toBe(false);
  });

  it('applies toSnapshot before comparing, ignoring fields it drops', () => {
    const model = signal({ enabled: true, uiOnly: 'a' });
    const tracker = createDirtyTracker(model, (m) => ({ enabled: m.enabled }));

    tracker.markSaved();
    model.set({ enabled: true, uiOnly: 'b' });

    expect(tracker.dirty()).toBe(false);
  });
});
