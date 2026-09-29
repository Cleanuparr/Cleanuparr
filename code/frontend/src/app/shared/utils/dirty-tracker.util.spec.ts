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

  it('markSaved(value) snapshots the passed value, not the model', () => {
    const model = signal({ enabled: true });
    const tracker = createDirtyTracker(model);

    tracker.markSaved({ enabled: false });
    expect(tracker.dirty()).toBe(true);
  });

  it('stays dirty when the model later diverges from a markSaved(value) snapshot', () => {
    const model = signal({ enabled: true });
    const tracker = createDirtyTracker(model);

    tracker.markSaved({ enabled: true });
    model.set({ enabled: false });

    expect(tracker.dirty()).toBe(true);
  });

  it('markSaved() with no argument still snapshots the model', () => {
    const model = signal({ enabled: true });
    const tracker = createDirtyTracker(model);

    tracker.markSaved();
    expect(tracker.dirty()).toBe(false);
  });
});
