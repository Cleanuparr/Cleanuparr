import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Subject, of, throwError } from 'rxjs';
import { SeekerApi } from '@core/api/seeker.api';
import { ConfirmService } from '@core/services/confirm.service';
import { ToastService } from '@core/services/toast.service';
import { ApiError } from '@core/interceptors/error.interceptor';
import { SeekerConfig, SeekerInstanceConfig } from '@shared/models/seeker-config.model';
import { SelectionStrategy } from '@shared/models/enums';
import { SeekerComponent } from './seeker.component';

const SONARR: SeekerInstanceConfig = {
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
};

const RADARR: SeekerInstanceConfig = {
  arrInstanceId: '22222222-2222-2222-2222-222222222222',
  instanceName: 'Radarr',
  instanceType: 'Radarr',
  enabled: false,
  skipTags: [],
  lastProcessedAt: '2026-08-01T10:00:00Z',
  arrInstanceEnabled: true,
  activeDownloadLimit: 5,
  ignoreStruckDownloads: true,
  minCycleTimeDays: 14,
  monitoredOnly: false,
  useCutoff: true,
  useCustomFormatScore: true,
};

const CONFIG: SeekerConfig = {
  searchEnabled: true,
  searchInterval: 5,
  proactiveSearchEnabled: true,
  selectionStrategy: SelectionStrategy.BalancedWeighted,
  useRoundRobin: true,
  postReleaseGraceHours: 6,
  instances: [SONARR, RADARR],
};

interface SetupOptions {
  config?: SeekerConfig;
  loadFails?: boolean;
  pending?: boolean;
  saveError?: ApiError;
}

interface Setup {
  fixture: ComponentFixture<SeekerComponent>;
  component: SeekerComponent;
  getConfig: ReturnType<typeof vi.fn>;
  updateConfig: ReturnType<typeof vi.fn>;
  confirm: ConfirmService;
  toasts: string[];
  pending: Subject<SeekerConfig>;
}

describe('SeekerComponent', () => {
  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  function setup(options: SetupOptions = {}): Setup {
    const config = options.config ?? CONFIG;
    const toasts: string[] = [];

    const pending = new Subject<SeekerConfig>();
    const getConfig = vi.fn(() => {
      if (options.loadFails) {
        return throwError(() => new Error('boom'));
      }
      return options.pending ? pending : of(config);
    });
    const updateConfig = vi.fn(() =>
      options.saveError ? throwError(() => options.saveError) : of(undefined),
    );

    TestBed.configureTestingModule({
      providers: [
        { provide: SeekerApi, useValue: { getConfig, updateConfig } },
        {
          provide: ToastService,
          useValue: {
            success: (message: string) => toasts.push(`success:${message}`),
            error: (message: string) => toasts.push(`error:${message}`),
          },
        },
      ],
    });

    const fixture = TestBed.createComponent(SeekerComponent);
    fixture.detectChanges();

    return {
      fixture,
      component: fixture.componentInstance,
      getConfig,
      updateConfig,
      confirm: TestBed.inject(ConfirmService),
      toasts,
      pending,
    };
  }

  function saveButton(fixture: ComponentFixture<SeekerComponent>): HTMLButtonElement {
    return fixture.nativeElement.querySelector('.form-actions button') as HTMLButtonElement;
  }

  function toggleByLabel(fixture: ComponentFixture<SeekerComponent>, label: string): HTMLButtonElement {
    return fixture.nativeElement.querySelector(`[aria-label="${label}"]`) as HTMLButtonElement;
  }

  function text(fixture: ComponentFixture<SeekerComponent>): string {
    return fixture.nativeElement.textContent as string;
  }

  function numberField(fixture: ComponentFixture<SeekerComponent>, label: string): HTMLInputElement {
    const host = Array.from(fixture.nativeElement.querySelectorAll('app-number-input')).find((input) =>
      (input as HTMLElement).querySelector('.number-label')?.textContent?.includes(label),
    ) as HTMLElement;
    return host.querySelector('.number-field') as HTMLInputElement;
  }

  function setNumberField(fixture: ComponentFixture<SeekerComponent>, label: string, value: string): void {
    const field = numberField(fixture, label);
    field.value = value;
    field.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  it('loads the config into the form and the instance rows', () => {
    const { fixture, component } = setup();

    expect(component.seekerForm.searchEnabled().value()).toBe(true);
    expect(component.seekerForm.searchInterval().value()).toBe(5);
    expect(component.seekerForm.proactiveSearchEnabled().value()).toBe(true);
    expect(component.seekerForm.selectionStrategy().value()).toBe(SelectionStrategy.BalancedWeighted);
    expect(component.seekerForm.useRoundRobin().value()).toBe(true);
    expect(component.seekerForm.postReleaseGraceHours().value()).toBe(6);

    expect(component.instances()).toEqual([
      { ...SONARR, skipTags: ['alpha', 'beta'] },
      { ...RADARR, skipTags: [] },
    ]);
    expect(component.dirty()).toBe(false);
    expect(component.hasPendingChanges()).toBe(false);
    expect(saveButton(fixture).disabled).toBe(true);
    expect(text(fixture)).toContain('Search for missing items and quality upgrades');
  });

  it('copies the loaded skip tags instead of sharing the response array', () => {
    const { component } = setup();

    component.patchInstance(0, { skipTags: ['gamma'] });

    expect(SONARR.skipTags).toEqual(['alpha', 'beta']);
    expect(component.instances()[0].skipTags).toEqual(['gamma']);
  });

  it('renders the skip tag chips and the per-instance detail controls', () => {
    const { fixture } = setup();

    const chips = Array.from(fixture.nativeElement.querySelectorAll('.chip')).map((chip) =>
      (chip as HTMLElement).textContent!.replace('×', '').trim(),
    );

    expect(chips).toEqual(['alpha', 'beta']);
    expect(toggleByLabel(fixture, 'Monitored Only')).not.toBeNull();
    expect(toggleByLabel(fixture, 'Use Cutoff')).not.toBeNull();
    expect(toggleByLabel(fixture, 'Use Custom Format Score')).not.toBeNull();
    expect(toggleByLabel(fixture, 'Ignore Struck Downloads')).not.toBeNull();
  });

  it('shows the empty state and refetches when the load fails', async () => {
    const { fixture, component, getConfig, toasts } = setup({ loadFails: true });

    expect(component.loadError()).toBe(true);
    expect(text(fixture)).toContain('Could not connect to server');
    expect(toasts).toContain('error:Failed to load seeker settings');
    expect(fixture.nativeElement.querySelector('.settings-form')).toBeNull();

    (fixture.nativeElement.querySelector('app-empty-state button') as HTMLButtonElement).click();
    await fixture.whenStable();

    expect(getConfig).toHaveBeenCalledTimes(2);
  });

  it('stops the deferred loader once the config resolves', () => {
    const { fixture, component } = setup();

    expect(component.loader.loading()).toBe(false);
    expect(component.loader.showSpinner()).toBe(false);
    expect(fixture.nativeElement.querySelector('app-loading-state')).toBeNull();
  });

  it('shows the spinner while the config is still in flight', async () => {
    vi.useFakeTimers();
    const { fixture, component, pending } = setup({ pending: true });

    expect(component.loader.loading()).toBe(true);
    expect(component.instances()).toEqual([]);

    vi.advanceTimersByTime(200);
    fixture.detectChanges();

    expect(component.loader.showSpinner()).toBe(true);
    expect(fixture.nativeElement.querySelector('app-loading-state')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.settings-form')).toBeNull();

    pending.next(CONFIG);
    pending.complete();
    vi.useRealTimers();
    await fixture.whenStable();

    expect(component.loader.loading()).toBe(false);
    expect(component.instances()).toHaveLength(2);
    expect(fixture.nativeElement.querySelector('.settings-form')).not.toBeNull();
  });

  it('hides the interval, proactive and instance cards while search is disabled', () => {
    const { fixture, component } = setup();

    component.seekerForm.searchEnabled().value.set(false);
    fixture.detectChanges();

    expect(text(fixture)).not.toContain('Search Interval');
    expect(text(fixture)).not.toContain('Proactive Search');
    expect(text(fixture)).not.toContain('Instances');
  });

  it('hides the proactive sub-settings and the instance card while proactive search is off', () => {
    const { fixture, component } = setup();

    component.seekerForm.proactiveSearchEnabled().value.set(false);
    fixture.detectChanges();

    expect(text(fixture)).not.toContain('Selection Strategy');
    expect(text(fixture)).not.toContain('Round Robin');
    expect(fixture.nativeElement.querySelector('.instance-list')).toBeNull();
  });

  it('hides the instance card when the config has no instances', () => {
    const { fixture } = setup({ config: { ...CONFIG, instances: [] } });

    expect(fixture.nativeElement.querySelector('.instance-list')).toBeNull();
    expect(text(fixture)).toContain('Selection Strategy');
  });

  it('describes the selected strategy', () => {
    const { fixture, component } = setup();

    expect(fixture.nativeElement.querySelector('.strategy-hint').textContent).toContain('Good default');

    component.seekerForm.selectionStrategy().value.set(SelectionStrategy.Random);
    fixture.detectChanges();

    expect(component.strategyDescription()).toBe('Every item has an equal chance of being picked. No prioritization.');
    expect(fixture.nativeElement.querySelector('.strategy-hint').textContent).toContain('equal chance');
  });

  it('drops the strategy hint for a strategy it does not describe', () => {
    const { fixture, component } = setup({
      config: { ...CONFIG, selectionStrategy: 'Unknown' as SelectionStrategy },
    });

    expect(component.strategyDescription()).toBe('');
    expect(fixture.nativeElement.querySelector('.strategy-hint')).toBeNull();
  });

  it('patches only the instance at the given index', () => {
    const { fixture, component } = setup();

    component.patchInstance(1, { minCycleTimeDays: 30, monitoredOnly: true });
    fixture.detectChanges();

    expect(component.instances()[0].minCycleTimeDays).toBe(7);
    expect(component.instances()[1].minCycleTimeDays).toBe(30);
    expect(component.instances()[1].monitoredOnly).toBe(true);
    expect(component.hasPendingChanges()).toBe(true);
  });

  it('flips the enabled instance toggle and reveals its details', () => {
    const { fixture, component } = setup();
    const radarrToggle = fixture.nativeElement.querySelectorAll('.instance-row__header [role="switch"]')[1] as HTMLButtonElement;

    expect(fixture.nativeElement.querySelectorAll('.instance-row__details')).toHaveLength(1);

    radarrToggle.click();
    fixture.detectChanges();

    expect(component.instances()[1].enabled).toBe(true);
    expect(fixture.nativeElement.querySelectorAll('.instance-row__details')).toHaveLength(2);

    radarrToggle.click();
    fixture.detectChanges();

    expect(component.instances()[1].enabled).toBe(false);
    expect(fixture.nativeElement.querySelectorAll('.instance-row__details')).toHaveLength(1);
  });

  it('warns and disables the toggle for an instance disabled in arr settings', () => {
    const { fixture } = setup({
      config: { ...CONFIG, instances: [{ ...SONARR, arrInstanceEnabled: false }] },
    });

    expect(text(fixture)).toContain('Enable this instance in Arr Settings first');
    expect((fixture.nativeElement.querySelector('.instance-row__header [role="switch"]') as HTMLButtonElement).disabled).toBe(true);
  });

  it('renders the last processed timestamp only for instances that have one', () => {
    const { fixture } = setup();

    const metas = fixture.nativeElement.querySelectorAll('.instance-row__meta');

    expect(metas).toHaveLength(1);
    expect((metas[0] as HTMLElement).textContent).toContain('Last processed:');
  });

  it('flips the struck download toggle through the DOM and saves it', () => {
    const { fixture, component, updateConfig } = setup();
    const toggle = toggleByLabel(fixture, 'Ignore Struck Downloads');

    expect(toggle.getAttribute('aria-checked')).toBe('false');

    toggle.click();
    fixture.detectChanges();

    expect(toggle.getAttribute('aria-checked')).toBe('true');
    expect(component.hasPendingChanges()).toBe(true);

    saveButton(fixture).click();
    fixture.detectChanges();

    expect(updateConfig.mock.calls[0][0].instances[0].ignoreStruckDownloads).toBe(true);
    expect(component.hasPendingChanges()).toBe(false);
  });

  it('patches the remaining instance toggles from the DOM', () => {
    const { fixture, component } = setup();

    toggleByLabel(fixture, 'Monitored Only').click();
    toggleByLabel(fixture, 'Use Cutoff').click();
    toggleByLabel(fixture, 'Use Custom Format Score').click();
    fixture.detectChanges();

    expect(component.instances()[0].monitoredOnly).toBe(false);
    expect(component.instances()[0].useCutoff).toBe(true);
    expect(component.instances()[0].useCustomFormatScore).toBe(true);
  });

  it('patches the instance limits typed into the numeric inputs', () => {
    const { fixture, component } = setup();

    setNumberField(fixture, 'Active Download Limit', '9');
    setNumberField(fixture, 'Min Cycle Time', '21');

    expect(component.instances()[0].activeDownloadLimit).toBe(9);
    expect(component.instances()[0].minCycleTimeDays).toBe(21);
  });

  it('falls back to the default limits when the numeric inputs are cleared', () => {
    const { fixture, component } = setup();

    setNumberField(fixture, 'Active Download Limit', '');
    setNumberField(fixture, 'Min Cycle Time', '');

    expect(component.instances()[0].activeDownloadLimit).toBe(3);
    expect(component.instances()[0].minCycleTimeDays).toBe(7);
  });

  it('patches the skip tags edited in the chip input', () => {
    const { fixture, component } = setup();
    const input = fixture.nativeElement.querySelector('.chip-input') as HTMLInputElement;

    input.value = 'gamma';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    fixture.detectChanges();

    expect(component.instances()[0].skipTags).toEqual(['alpha', 'beta', 'gamma']);

    (fixture.nativeElement.querySelector('.chip__remove') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(component.instances()[0].skipTags).toEqual(['beta', 'gamma']);
  });

  it('blocks saving while proactive search has no enabled instance', () => {
    const { fixture, component } = setup();

    component.toggleInstance(0);
    fixture.detectChanges();

    expect(component.instanceError()).toBe('At least one instance must be enabled when proactive search is enabled');
    expect(fixture.nativeElement.querySelector('.instance-error').textContent).toContain('At least one instance');
    expect(component.hasErrors()).toBe(true);
    expect(saveButton(fixture).disabled).toBe(true);

    component.seekerForm.proactiveSearchEnabled().value.set(false);
    fixture.detectChanges();

    expect(component.instanceError()).toBeUndefined();
    expect(component.hasErrors()).toBe(false);
    expect(saveButton(fixture).disabled).toBe(false);
  });

  it('blocks saving while the grace period is out of range', () => {
    const { fixture, component } = setup();

    component.seekerForm.postReleaseGraceHours().value.set(100);
    fixture.detectChanges();

    expect(component.hasErrors()).toBe(true);
    expect(saveButton(fixture).disabled).toBe(true);

    component.seekerForm.postReleaseGraceHours().value.set(12);
    fixture.detectChanges();

    expect(component.hasErrors()).toBe(false);
    expect(saveButton(fixture).disabled).toBe(false);
  });

  it('confirms before turning round robin off and leaves it on when cancelled', async () => {
    const { fixture, component, confirm } = setup();

    const cancelled = component.confirmRoundRobin(false);
    expect(confirm.state()?.title).toBe('Disable Round Robin');
    confirm.cancel();

    expect(await cancelled).toBe(false);

    toggleByLabel(fixture, 'Round Robin').click();
    confirm.accept();
    await fixture.whenStable();

    expect(component.seekerForm.useRoundRobin().value()).toBe(false);
    expect(await component.confirmRoundRobin(true)).toBe(true);
    expect(confirm.state()).toBeNull();
  });

  it('posts every edited field and goes clean again', () => {
    const { fixture, component, updateConfig, toasts } = setup();

    component.seekerForm.searchInterval().value.set(10);
    component.seekerForm.selectionStrategy().value.set(SelectionStrategy.NewestFirst);
    component.seekerForm.postReleaseGraceHours().value.set(24);
    component.patchInstance(0, { skipTags: ['gamma'], ignoreStruckDownloads: true });
    fixture.detectChanges();

    expect(component.dirty()).toBe(true);

    saveButton(fixture).click();
    fixture.detectChanges();

    expect(updateConfig).toHaveBeenCalledWith({
      searchEnabled: true,
      searchInterval: 10,
      proactiveSearchEnabled: true,
      selectionStrategy: SelectionStrategy.NewestFirst,
      useRoundRobin: true,
      postReleaseGraceHours: 24,
      instances: [
        {
          arrInstanceId: SONARR.arrInstanceId,
          enabled: true,
          skipTags: ['gamma'],
          activeDownloadLimit: 3,
          ignoreStruckDownloads: true,
          minCycleTimeDays: 7,
          monitoredOnly: true,
          useCutoff: false,
          useCustomFormatScore: false,
        },
        {
          arrInstanceId: RADARR.arrInstanceId,
          enabled: false,
          skipTags: [],
          activeDownloadLimit: 5,
          ignoreStruckDownloads: true,
          minCycleTimeDays: 14,
          monitoredOnly: false,
          useCutoff: true,
          useCustomFormatScore: true,
        },
      ],
    });
    expect(component.dirty()).toBe(false);
    expect(component.saving()).toBe(false);
    expect(component.saved()).toBe(true);
    expect(toasts).toContain('success:Seeker settings saved');
    expect(text(fixture)).toContain('Saved!');
  });

  it('falls back to the default interval and grace period on cleared inputs', () => {
    const { fixture, component, updateConfig } = setup();

    component.seekerForm.searchInterval().value.set(null as unknown as number);
    component.seekerForm.postReleaseGraceHours().value.set(null);
    fixture.detectChanges();

    component.save();

    expect(updateConfig).toHaveBeenCalledWith(
      expect.objectContaining({ searchInterval: 2, postReleaseGraceHours: 6 }),
    );
  });

  it('clears the saved label after the confirmation delay', () => {
    vi.useFakeTimers();
    const { fixture, component } = setup();

    component.patchInstance(0, { ignoreStruckDownloads: true });
    component.save();
    fixture.detectChanges();

    expect(component.saved()).toBe(true);

    vi.advanceTimersByTime(1500);
    fixture.detectChanges();

    expect(component.saved()).toBe(false);
    expect(text(fixture)).toContain('Save Settings');
  });

  it('reports the server message on a rejected save and stays dirty', () => {
    const saveError = Object.assign(new ApiError('Active download limit must be positive'), { statusCode: 400 });
    const { fixture, component, toasts } = setup({ saveError });

    component.patchInstance(0, { ignoreStruckDownloads: true });
    component.save();
    fixture.detectChanges();

    expect(toasts).toContain('error:Active download limit must be positive');
    expect(component.saving()).toBe(false);
    expect(component.saved()).toBe(false);
    expect(component.hasPendingChanges()).toBe(true);
  });

  it('reports a generic message when the save fails for another reason', () => {
    const saveError = Object.assign(new ApiError('Internal Server Error'), { statusCode: 500 });
    const { component, toasts } = setup({ saveError });

    component.patchInstance(0, { ignoreStruckDownloads: true });
    component.save();

    expect(toasts).toContain('error:Failed to save seeker settings');
  });

  it('maps the instance type to an icon and a badge severity', () => {
    const { fixture, component } = setup();

    expect(component.getInstanceIcon('Sonarr')).toBe('icons/ext/sonarr-light.svg');
    expect(component.getInstanceIcon('Radarr')).toBe('icons/ext/radarr-light.svg');
    expect(component.getInstanceTypeSeverity('Sonarr')).toBe('info');
    expect(component.getInstanceTypeSeverity('Radarr')).toBe('warning');
    expect(component.getInstanceTypeSeverity('Lidarr')).toBe('default');

    const icons = fixture.nativeElement.querySelectorAll('.instance-row__icon');
    expect((icons[0] as HTMLImageElement).getAttribute('src')).toBe('icons/ext/sonarr-light.svg');
    expect((icons[1] as HTMLImageElement).getAttribute('src')).toBe('icons/ext/radarr-light.svg');
  });
});
