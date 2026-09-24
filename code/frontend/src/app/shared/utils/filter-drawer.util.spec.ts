import { createFilterDrawer } from './filter-drawer.util';

interface Filters extends Record<string, unknown> {
  search: string;
  status: string;
}

const EMPTY: Filters = { search: '', status: '' };

describe('createFilterDrawer', () => {
  it('starts closed with empty applied and draft filters', () => {
    const drawer = createFilterDrawer(EMPTY);

    expect(drawer.applied()).toEqual(EMPTY);
    expect(drawer.draft()).toEqual(EMPTY);
    expect(drawer.drawerOpen()).toBe(false);
    expect(drawer.activeCount()).toBe(0);
  });

  it('open() seeds the draft from applied and opens the drawer', () => {
    const drawer = createFilterDrawer(EMPTY);
    drawer.open();
    drawer.updateDraft('search', 'foo');
    drawer.apply();

    drawer.updateDraft('search', 'stale draft value');
    drawer.open();

    expect(drawer.draft()).toEqual({ search: 'foo', status: '' });
    expect(drawer.drawerOpen()).toBe(true);
  });

  it('updateDraft() updates one field without touching the rest', () => {
    const drawer = createFilterDrawer(EMPTY);

    drawer.updateDraft('search', 'bar');

    expect(drawer.draft()).toEqual({ search: 'bar', status: '' });
  });

  it('reset() clears the draft back to empty', () => {
    const drawer = createFilterDrawer(EMPTY);
    drawer.updateDraft('search', 'bar');

    drawer.reset();

    expect(drawer.draft()).toEqual(EMPTY);
  });

  it('apply() commits the draft as applied and closes the drawer', () => {
    const drawer = createFilterDrawer(EMPTY);
    drawer.open();
    drawer.updateDraft('status', 'active');

    drawer.apply();

    expect(drawer.applied()).toEqual({ search: '', status: 'active' });
    expect(drawer.drawerOpen()).toBe(false);
  });

  it('activeCount() counts applied fields that differ from empty', () => {
    const drawer = createFilterDrawer(EMPTY);
    drawer.open();
    drawer.updateDraft('search', 'foo');
    drawer.updateDraft('status', 'active');
    drawer.apply();

    expect(drawer.activeCount()).toBe(2);
  });
});
