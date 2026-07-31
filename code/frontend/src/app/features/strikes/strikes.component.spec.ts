import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { StrikesApi } from '@core/api/strikes.api';
import { ConfirmService } from '@core/services/confirm.service';
import { DownloadItemStrikes, StrikeFilter } from '@core/models/strike.models';
import { PaginatedResult } from '@core/models/pagination.model';
import { StrikesComponent } from './strikes.component';

const PAGE_SIZE_KEY = 'cleanuparr-page-size-strikes';

class IntersectionObserverStub {
  observe(): void {
    return undefined;
  }

  unobserve(): void {
    return undefined;
  }

  disconnect(): void {
    return undefined;
  }
}

const ITEMS: DownloadItemStrikes[] = [
  {
    downloadItemId: 'item-1',
    downloadId: 'hash-1',
    title: 'Some Release 1080p',
    isMarkedForRemoval: false,
    isRemoved: false,
    isReturning: false,
    hasDryRunStrikes: false,
    totalStrikes: 3,
    strikesByType: { Stalled: 2, SlowSpeed: 1 },
    latestStrikeAt: '2026-07-30T12:00:00Z',
    firstStrikeAt: '2026-07-30T10:00:00Z',
    strikes: [
      {
        id: 'strike-1',
        type: 'SlowSpeed',
        createdAt: '2026-07-30T12:00:00Z',
        lastDownloadedBytes: 1536,
        jobRunId: 'run-a',
        isDryRun: false,
      },
    ],
  },
  {
    downloadItemId: 'item-2',
    downloadId: 'hash-2',
    title: 'Another Release',
    isMarkedForRemoval: true,
    isRemoved: false,
    isReturning: false,
    hasDryRunStrikes: false,
    totalStrikes: 1,
    strikesByType: { FailedImport: 1 },
    latestStrikeAt: '2026-07-30T11:00:00Z',
    firstStrikeAt: '2026-07-30T11:00:00Z',
    strikes: [
      {
        id: 'strike-2',
        type: 'FailedImport',
        createdAt: '2026-07-30T11:00:00Z',
        lastDownloadedBytes: null,
        jobRunId: 'run-b',
        isDryRun: true,
      },
    ],
  },
];

interface Harness {
  fixture: ComponentFixture<StrikesComponent>;
  filters: StrikeFilter[];
  deleted: string[];
}

describe('StrikesComponent', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.useRealTimers();
    localStorage.clear();
  });

  function setup(options: { confirmed?: boolean; storedPageSize?: string; totalCount?: number } = {}): Harness {
    localStorage.clear();
    if (options.storedPageSize !== undefined) {
      localStorage.setItem(PAGE_SIZE_KEY, options.storedPageSize);
    }
    vi.stubGlobal('IntersectionObserver', IntersectionObserverStub);

    const filters: StrikeFilter[] = [];
    const deleted: string[] = [];

    TestBed.configureTestingModule({
      providers: [
        {
          provide: StrikesApi,
          useValue: {
            getStrikes: (filter: StrikeFilter) => {
              filters.push(filter);
              return of({
                items: ITEMS,
                page: filter.page ?? 1,
                pageSize: filter.pageSize ?? 50,
                totalCount: options.totalCount ?? ITEMS.length,
                totalPages: 1,
              } as PaginatedResult<DownloadItemStrikes>);
            },
            getStrikeTypes: () => of(['FailedImport', 'SlowSpeed', 'Stalled']),
            deleteStrikesForItem: (id: string) => {
              deleted.push(id);
              return of(undefined);
            },
          },
        },
        {
          provide: ConfirmService,
          useValue: { confirm: () => Promise.resolve(options.confirmed ?? true) },
        },
      ],
    });

    const fixture = TestBed.createComponent(StrikesComponent);
    fixture.detectChanges();
    return { fixture, filters, deleted };
  }

  function lastFilter(filters: StrikeFilter[]): StrikeFilter {
    return filters[filters.length - 1];
  }

  it('sends only the populated filters to the api', () => {
    const { fixture, filters } = setup();
    const component = fixture.componentInstance;

    expect(filters).toEqual([{ page: 1, pageSize: 50 }]);

    component.selectedType.set('Stalled');
    component.searchQuery.set('release');
    fixture.detectChanges();

    expect(lastFilter(filters)).toEqual({ page: 1, pageSize: 50, type: 'Stalled', search: 'release' });

    component.selectedType.set('');
    component.searchQuery.set('');
    fixture.detectChanges();

    expect(lastFilter(filters)).toEqual({ page: 1, pageSize: 50 });
  });

  it('returns to the first page when a filter changes after paging', () => {
    const { fixture, filters } = setup({ totalCount: 120 });
    const component = fixture.componentInstance;

    component.onPageChange(3);
    fixture.detectChanges();
    expect(lastFilter(filters).page).toBe(3);

    component.selectedType.set('Stalled');
    component.onFilterChange();
    fixture.detectChanges();

    expect(component.currentPage()).toBe(1);
    expect(lastFilter(filters)).toEqual({ page: 1, pageSize: 50, type: 'Stalled' });
  });

  it('persists a valid page size and ignores an invalid one', () => {
    const { fixture, filters } = setup({ totalCount: 120 });
    const component = fixture.componentInstance;

    component.onPageChange(3);
    fixture.detectChanges();

    component.onPageSizeChange(25);
    fixture.detectChanges();

    expect(localStorage.getItem(PAGE_SIZE_KEY)).toBe('25');
    expect(component.pageSize()).toBe(25);
    expect(component.currentPage()).toBe(1);
    expect(lastFilter(filters)).toEqual({ page: 1, pageSize: 25 });

    component.onPageSizeChange(0);
    fixture.detectChanges();

    expect(localStorage.getItem(PAGE_SIZE_KEY)).toBe('25');
    expect(component.pageSize()).toBe(25);
  });

  it('starts from the page size stored for this page', () => {
    const { fixture, filters } = setup({ storedPageSize: '10' });

    expect(fixture.componentInstance.pageSize()).toBe(10);
    expect(filters[0].pageSize).toBe(10);
  });

  it('prepends an all-types entry and spaces out the type names', () => {
    const { fixture } = setup();

    expect(fixture.componentInstance.typeOptions()).toEqual([
      { label: 'All Types', value: '' },
      { label: 'Failed Import', value: 'FailedImport' },
      { label: 'Slow Speed', value: 'SlowSpeed' },
      { label: 'Stalled', value: 'Stalled' },
    ]);
  });

  it('expands a single row at a time and renders its formatted byte count', () => {
    const { fixture } = setup();
    const component = fixture.componentInstance;

    component.toggleExpand('item-1');
    fixture.detectChanges();

    expect(component.expandedId()).toBe('item-1');
    expect(fixture.nativeElement.querySelectorAll('.strike-row--expanded')).toHaveLength(1);
    expect(
      (fixture.nativeElement.querySelector('.strike-table__bytes') as HTMLElement).textContent!.trim(),
    ).toBe('1.5 KB');

    component.toggleExpand('item-2');
    fixture.detectChanges();
    expect(component.expandedId()).toBe('item-2');
    expect(
      (fixture.nativeElement.querySelector('.strike-table__bytes') as HTMLElement).textContent!.trim(),
    ).toBe('-');

    component.toggleExpand('item-2');
    fixture.detectChanges();
    expect(component.expandedId()).toBeNull();
    expect(fixture.nativeElement.querySelectorAll('.strike-row--expanded')).toHaveLength(0);
  });

  it('deletes the strikes of an item and reloads the list once confirmed', async () => {
    const { fixture, filters, deleted } = setup({ confirmed: true });
    const before = filters.length;

    await fixture.componentInstance.deleteItemStrikes(ITEMS[0]);
    fixture.detectChanges();

    expect(deleted).toEqual(['item-1']);
    expect(filters.length).toBe(before + 1);
  });

  it('deletes nothing when the confirmation is dismissed', async () => {
    const { fixture, filters, deleted } = setup({ confirmed: false });
    const before = filters.length;

    await fixture.componentInstance.deleteItemStrikes(ITEMS[0]);
    fixture.detectChanges();

    expect(deleted).toEqual([]);
    expect(filters.length).toBe(before);
  });

  it('polls the list while alive and stops polling once destroyed', () => {
    vi.useFakeTimers();
    const { fixture, filters } = setup();
    const afterInit = filters.length;

    vi.advanceTimersByTime(10_000);
    fixture.detectChanges();
    expect(filters.length).toBe(afterInit + 1);

    fixture.destroy();
    vi.advanceTimersByTime(30_000);

    expect(filters.length).toBe(afterInit + 1);
  });
});
