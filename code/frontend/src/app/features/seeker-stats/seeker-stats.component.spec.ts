import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';
import { CfScoreApi } from '@core/api/cf-score.api';
import { SearchStatsApi } from '@core/api/search-stats.api';
import { AppHubService } from '@core/realtime/app-hub.service';
import { SeekerStatsComponent } from './seeker-stats.component';

const EMPTY_PAGE = { items: [], page: 1, pageSize: 50, totalCount: 0, totalPages: 0 };

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

interface Harness {
  fixture: ComponentFixture<SeekerStatsComponent>;
  component: SeekerStatsComponent;
  navigate: ReturnType<typeof vi.fn>;
  route: ActivatedRoute;
}

describe('SeekerStatsComponent', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  function setup(queryParams: Record<string, string> = {}): Harness {
    vi.stubGlobal('IntersectionObserver', IntersectionObserverStub);
    const navigate = vi.fn();
    const route = { snapshot: { queryParamMap: convertToParamMap(queryParams) } } as ActivatedRoute;

    TestBed.configureTestingModule({
      providers: [
        {
          provide: SearchStatsApi,
          useValue: {
            getSummary: () => of(null),
            getEvents: () => of(EMPTY_PAGE),
          },
        },
        {
          provide: CfScoreApi,
          useValue: {
            getStats: () => of(null),
            getInstances: () => of({ instances: [] }),
            getScores: () => of(EMPTY_PAGE),
            getRecentUpgrades: () => of(EMPTY_PAGE),
          },
        },
        {
          provide: AppHubService,
          useValue: { searchStatsVersion: signal(0), cfScoresVersion: signal(0) },
        },
        { provide: ActivatedRoute, useValue: route },
        { provide: Router, useValue: { navigate } },
      ],
    });

    const fixture = TestBed.createComponent(SeekerStatsComponent);
    fixture.detectChanges();

    return { fixture, component: fixture.componentInstance, navigate, route };
  }

  function renderedTabs(fixture: ComponentFixture<SeekerStatsComponent>): string[] {
    return ['app-searches-tab', 'app-quality-tab', 'app-upgrades-tab'].filter(
      (selector) => fixture.nativeElement.querySelector(selector) !== null,
    );
  }

  it('renders the searches tab by default', () => {
    const { fixture, component } = setup();

    expect(component.activeTab()).toBe('searches');
    expect(renderedTabs(fixture)).toEqual(['app-searches-tab']);
  });

  it('opens the tab named in the query parameters', () => {
    const { fixture, component } = setup({ tab: 'upgrades' });

    expect(component.activeTab()).toBe('upgrades');
    expect(renderedTabs(fixture)).toEqual(['app-upgrades-tab']);
  });

  it('ignores an unknown tab in the query parameters', () => {
    const { fixture, component } = setup({ tab: 'nonsense' });

    expect(component.activeTab()).toBe('searches');
    expect(renderedTabs(fixture)).toEqual(['app-searches-tab']);
  });

  it('swaps the rendered tab and records the selection in the query parameters', () => {
    const { fixture, component, navigate, route } = setup();

    component.onTabChange('quality');
    fixture.detectChanges();

    expect(renderedTabs(fixture)).toEqual(['app-quality-tab']);
    expect(navigate).toHaveBeenCalledWith([], {
      relativeTo: route,
      queryParams: { tab: 'quality' },
      queryParamsHandling: 'merge',
    });
  });
});
