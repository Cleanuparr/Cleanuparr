import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { SeekerApi } from '@core/api/seeker.api';
import { SeekerConfig } from '@shared/models/seeker-config.model';
import { SelectionStrategy } from '@shared/models/enums';
import { SeekerComponent } from './seeker.component';

const CONFIG: SeekerConfig = {
  searchEnabled: true,
  searchInterval: 5,
  proactiveSearchEnabled: true,
  selectionStrategy: SelectionStrategy.BalancedWeighted,
  useRoundRobin: true,
  postReleaseGraceHours: 6,
  instances: [
    {
      arrInstanceId: '11111111-1111-1111-1111-111111111111',
      instanceName: 'Sonarr',
      instanceType: 'Sonarr',
      enabled: true,
      skipTags: ['alpha', 'beta'],
      arrInstanceEnabled: true,
      activeDownloadLimit: 3,
      ignoreStruckDownloads: false,
      minCycleTimeDays: 7,
      monitoredOnly: true,
      useCutoff: false,
      useCustomFormatScore: false,
    },
  ],
};

describe('SeekerComponent', () => {
  function setup(): ComponentFixture<SeekerComponent> {
    TestBed.configureTestingModule({
      providers: [
        {
          provide: SeekerApi,
          useValue: {
            getConfig: () => of(CONFIG),
            updateConfig: () => of(undefined),
          },
        },
      ],
    });

    const fixture = TestBed.createComponent(SeekerComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('loads skip tags for an instance and reports no pending changes', () => {
    const fixture = setup();

    const chips = Array.from(fixture.nativeElement.querySelectorAll('.chip')).map((chip) =>
      (chip as HTMLElement).textContent!.replace('×', '').trim(),
    );

    expect(chips).toEqual(['alpha', 'beta']);
    expect(fixture.componentInstance.instances()[0].skipTags).toEqual(['alpha', 'beta']);
    expect(fixture.componentInstance.hasPendingChanges()).toBe(false);
  });

  it('marks pending changes when the struck download toggle flips', () => {
    const fixture = setup();

    fixture.componentInstance.updateInstanceIgnoreStruckDownloads(0, true);
    fixture.detectChanges();

    expect(fixture.componentInstance.instances()[0].ignoreStruckDownloads).toBe(true);
    expect(fixture.componentInstance.hasPendingChanges()).toBe(true);
  });
});
