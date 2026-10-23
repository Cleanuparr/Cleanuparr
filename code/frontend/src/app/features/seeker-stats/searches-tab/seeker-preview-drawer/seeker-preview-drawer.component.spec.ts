import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NEVER, of, throwError } from 'rxjs';
import { SearchStatsApi } from '@core/api/search-stats.api';
import { SeekerContentKind, SeekerExclusionReason, SeekerSearchReason } from '@core/models/search-stats.models';
import type { InstanceFunnel, InstanceSearchStat, SeekerPreview, SeekerPreviewExclusion } from '@core/models/search-stats.models';
import { SeekerPreviewDrawerComponent } from './seeker-preview-drawer.component';

const FUNNEL: InstanceFunnel = {
  libraryItems: 100,
  releasedItems: 90,
  monitoredItems: 80,
  tagFilteredItems: 70,
  eligibleUnits: 60,
  queuedUnits: 5,
  recentlyGrabbedUnits: 4,
  restingUnits: 0,
  searchedUnits: 30,
  remainingUnits: 21,
  countedAt: '2026-07-30T10:00:00Z',
};

const INSTANCE: InstanceSearchStat = {
  instanceId: 'instance-1',
  instanceName: 'Main Sonarr',
  instanceType: 'Sonarr',
  itemsTracked: 10,
  totalSearchCount: 5,
  lastSearchedAt: null,
  lastProcessedAt: null,
  currentCycleId: null,
  cycleItemsSearched: 0,
  cycleItemsTotal: 0,
  cycleStartedAt: null,
  cycleItemsRemaining: 0,
  cycleCompletedAt: null,
  lastCycleStartedAt: null,
  lastCycleCompletedAt: null,
  funnel: null,
};

function preview(partial: Partial<SeekerPreview> = {}): SeekerPreview {
  return {
    funnel: FUNNEL,
    cycleWaitingUntil: null,
    randomPick: false,
    nextUp: [
      { name: 'Alpha S01E01', reason: SeekerSearchReason.Missing, contentKind: null },
      { name: 'Beta S02E03', reason: SeekerSearchReason.QualityCutoffNotMet, contentKind: SeekerContentKind.Anime },
    ],
    excluded: [
      { name: 'Gamma Show', reason: SeekerExclusionReason.NotMonitored },
      { name: 'delta show', reason: SeekerExclusionReason.InQueue },
      { name: 'Epsilon', reason: SeekerExclusionReason.NotMonitored },
    ],
    ...partial,
  };
}

function manyExcluded(count: number): SeekerPreviewExclusion[] {
  return Array.from({ length: count }, (_, i) => ({
    name: `Item ${String(i + 1).padStart(2, '0')}`,
    reason: SeekerExclusionReason.NotMonitored,
  }));
}

@Component({
  selector: 'app-host',
  standalone: true,
  imports: [SeekerPreviewDrawerComponent],
  template: `<app-seeker-preview-drawer [instance]="instance()" [(visible)]="visible" />`,
})
class HostComponent {
  readonly instance = signal<InstanceSearchStat | null>(INSTANCE);
  readonly visible = signal(false);
}

describe('SeekerPreviewDrawerComponent', () => {
  function setup(response: ReturnType<SearchStatsApi['getPreview']> = of(preview())) {
    const getPreview = vi.fn<(instanceId: string) => typeof response>(() => response);
    TestBed.configureTestingModule({
      providers: [{ provide: SearchStatsApi, useValue: { getPreview } }],
    });
    const fixture: ComponentFixture<HostComponent> = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    return { fixture, host: fixture.componentInstance, getPreview };
  }

  function open(fixture: ComponentFixture<HostComponent>, host: HostComponent): SeekerPreviewDrawerComponent {
    host.visible.set(true);
    fixture.detectChanges();
    fixture.detectChanges();
    return fixture.debugElement.children[0].componentInstance as SeekerPreviewDrawerComponent;
  }

  function texts(selector: string): string[] {
    return Array.from(document.body.querySelectorAll(selector)).map((el) => el.textContent!.trim());
  }

  function names(selector: string): string[] {
    return Array.from(document.body.querySelectorAll(`${selector} .preview__name`)).map((el) => el.textContent!.trim());
  }

  afterEach(() => {
    TestBed.resetTestingModule();
    document.body.querySelectorAll('.cdk-overlay-container').forEach((el) => {
      el.replaceChildren();
    });
  });

  it('does not request the preview while hidden', () => {
    const { fixture, getPreview } = setup();

    fixture.detectChanges();

    expect(getPreview).not.toHaveBeenCalled();
  });

  it('renders the label and detail line for a resting exclusion', () => {
    const { fixture, host } = setup(of(preview({
      excluded: [
        { name: 'Zeta', reason: SeekerExclusionReason.Resting, consecutiveMisses: 5, restCyclesLeft: 2, restUntil: '2026-08-15T12:00:00Z' },
        { name: 'Eta', reason: SeekerExclusionReason.Resting, consecutiveMisses: 3, restCyclesLeft: 1 },
        { name: 'Theta', reason: SeekerExclusionReason.NotMonitored },
      ],
    })));

    open(fixture, host);

    expect(texts('.preview__row--excluded app-badge')).toEqual([
      'Resting after repeated misses',
      'Resting after repeated misses',
      'Not monitored',
    ]);
    expect(texts('.preview__detail')).toEqual([
      '5 misses in a row · back after 2 cycles end or on 08/15',
      '3 misses in a row · back after 1 cycle end',
    ]);
  });

  it('renders no detail line for a resting exclusion without rest fields', () => {
    const { fixture, host } = setup(of(preview({
      excluded: [{ name: 'Iota', reason: SeekerExclusionReason.Resting }],
    })));

    open(fixture, host);

    expect(texts('.preview__detail')).toEqual([]);
  });

  it('requests the preview for the instance when opened', () => {
    const { fixture, host, getPreview } = setup();

    open(fixture, host);

    expect(getPreview).toHaveBeenCalledTimes(1);
    expect(getPreview).toHaveBeenCalledWith('instance-1');
  });

  it('renders the funnel stages', () => {
    const { fixture, host } = setup();

    open(fixture, host);

    const stages = texts('.preview__funnel-stage');
    expect(stages[0]).toContain('100');
    expect(stages[0]).toContain('series');
    expect(stages).toHaveLength(9);
  });

  it('lists the next up rows with a reason badge', () => {
    const { fixture, host } = setup();

    open(fixture, host);

    expect(names('.preview__row--next')).toEqual(['Alpha S01E01', 'Beta S02E03']);
    const rows = texts('.preview__row--next');
    expect(rows[0]).toContain('Missing');
    expect(rows[1]).toContain('Cutoff Unmet');
  });

  it('shows the random draw note only when randomPick is set', () => {
    const first = setup(of(preview({ randomPick: true })));
    open(first.fixture, first.host);
    expect(document.body.textContent).toContain('Random draw');

    TestBed.resetTestingModule();
    document.body.querySelectorAll('.cdk-overlay-container').forEach((el) => {
      el.replaceChildren();
    });

    const second = setup(of(preview({ randomPick: false })));
    open(second.fixture, second.host);
    expect(document.body.textContent).not.toContain('Random draw');
  });

  it('replaces the next up list with the waiting note when the cycle is waiting', () => {
    const { fixture, host } = setup(of(preview({ cycleWaitingUntil: '2026-08-01T10:00:00Z' })));

    open(fixture, host);

    expect(document.body.querySelector('.preview__note--waiting')).not.toBeNull();
    expect(document.body.querySelectorAll('.preview__row--next')).toHaveLength(0);
  });

  it('narrows the excluded list by reason', () => {
    const { fixture, host } = setup();
    const drawer = open(fixture, host);

    expect(names('.preview__row--excluded')).toHaveLength(3);

    drawer.onReasonChange(SeekerExclusionReason.InQueue);
    fixture.detectChanges();

    expect(names('.preview__row--excluded')).toEqual(['delta show']);
  });

  it('narrows the excluded list by title ignoring case', () => {
    const { fixture, host } = setup();
    const drawer = open(fixture, host);

    drawer.titleFilter.set('SHOW');
    fixture.detectChanges();

    expect(names('.preview__row--excluded')).toEqual(['Gamma Show', 'delta show']);
  });

  it('pages the excluded list at 25 per page', () => {
    const { fixture, host } = setup(of(preview({ excluded: manyExcluded(30) })));
    open(fixture, host);

    expect(names('.preview__row--excluded')).toHaveLength(25);

    const next = Array.from(document.body.querySelectorAll('.paginator__controls button')).find(
      (button) => button.textContent!.trim() === 'Next',
    ) as HTMLButtonElement;
    next.click();
    fixture.detectChanges();

    expect(names('.preview__row--excluded')).toEqual(manyExcluded(30).slice(25).map((item) => item.name));
  });

  it('shows the error message when the request fails', () => {
    const { fixture, host } = setup(throwError(() => new Error('preview unavailable')));

    open(fixture, host);

    expect(document.body.textContent).toContain('Preview failed');
    expect(document.body.textContent).toContain('preview unavailable');
  });

  it('reloads the preview when Refresh is clicked', () => {
    const { fixture, host, getPreview } = setup();
    open(fixture, host);

    const refresh = Array.from(document.body.querySelectorAll('button')).find(
      (button) => button.textContent!.trim() === 'Refresh',
    ) as HTMLButtonElement;
    refresh.click();
    fixture.detectChanges();

    expect(getPreview).toHaveBeenCalledTimes(2);
  });

  it('shows a spinner while the preview loads', () => {
    const { fixture, host } = setup(NEVER);

    open(fixture, host);

    expect(document.body.querySelector('app-spinner')).not.toBeNull();
  });

  it('shows the empty note when nothing is left to search', () => {
    const { fixture, host } = setup(of(preview({ nextUp: [] })));

    open(fixture, host);

    expect(document.body.querySelector('.preview__note')!.textContent).toContain('Nothing left to search.');
  });

  it('filters the excluded list from the title input and shows a note when nothing matches', () => {
    const { fixture, host } = setup();
    open(fixture, host);
    const input = document.body.querySelector('input[type="search"]') as HTMLInputElement;

    input.value = 'gamma';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(names('.preview__row--excluded')).toEqual(['Gamma Show']);

    input.value = 'zzz';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(names('.preview__row--excluded')).toEqual([]);
    expect(document.body.textContent).toContain('No excluded items match.');
  });

  it('titles the drawer Preview when no instance is set', () => {
    const { fixture, host } = setup();
    host.instance.set(null);

    open(fixture, host);

    expect(document.body.querySelector('.drawer__title')!.textContent!.trim()).toBe('Preview');
  });
});
