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
  let updateConfig: ReturnType<typeof vi.fn>;

  function setup(): ComponentFixture<SeekerComponent> {
    updateConfig = vi.fn(() => of(undefined));

    TestBed.configureTestingModule({
      providers: [
        {
          provide: SeekerApi,
          useValue: {
            getConfig: () => of(CONFIG),
            updateConfig,
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

  it('saves the struck download toggle flipped through the DOM', () => {
    const fixture = setup();
    const toggle = fixture.nativeElement.querySelector(
      '[aria-label="Ignore Struck Downloads"]',
    ) as HTMLButtonElement;

    expect(toggle.getAttribute('aria-checked')).toBe('false');

    toggle.click();
    fixture.detectChanges();

    expect(toggle.getAttribute('aria-checked')).toBe('true');
    expect(fixture.componentInstance.hasPendingChanges()).toBe(true);

    saveButton(fixture).click();
    fixture.detectChanges();

    expect(updateConfig).toHaveBeenCalledTimes(1);
    expect(updateConfig.mock.calls[0][0].instances[0].ignoreStruckDownloads).toBe(true);
    expect(fixture.componentInstance.hasPendingChanges()).toBe(false);
  });

  function saveButton(fixture: ComponentFixture<SeekerComponent>): HTMLButtonElement {
    return Array.from(fixture.nativeElement.querySelectorAll('button')).find((button) =>
      (button as HTMLElement).textContent!.includes('Save Settings'),
    ) as HTMLButtonElement;
  }
});
