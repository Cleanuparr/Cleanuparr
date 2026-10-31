import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { ArrApi } from '@core/api/arr.api';
import { ConfirmService } from '@core/services/confirm.service';
import { ThemeService } from '@core/services/theme.service';
import { ToastService } from '@core/services/toast.service';
import { FeatureBadgeService } from '@core/feature-badges/feature-badge.service';
import { ArrConfig, ArrInstance } from '@shared/models/arr-config.model';
import { ArrAppsComponent } from './arr-apps.component';

const SONARR_INSTANCE: ArrInstance = {
  id: 'sonarr-1',
  enabled: true,
  name: 'Main Sonarr',
  url: 'http://localhost:8989',
  externalUrl: 'https://sonarr.example.com',
  apiKey: 'sonarr-key',
  version: 4,
};

const RADARR_INSTANCE: ArrInstance = {
  id: 'radarr-1',
  enabled: true,
  name: 'Main Radarr',
  url: 'http://localhost:7878',
  apiKey: 'radarr-key',
  version: 6,
};

const SONARR_CONFIG: ArrConfig = {
  failedImportMaxStrikes: 3,
  instances: [SONARR_INSTANCE],
};

const RADARR_CONFIG: ArrConfig = {
  failedImportMaxStrikes: -1,
  instances: [RADARR_INSTANCE],
};

const EMPTY_CONFIG: ArrConfig = {
  failedImportMaxStrikes: -1,
  instances: [],
};

function createApi(configs: ArrConfig[] = [
  SONARR_CONFIG, RADARR_CONFIG, EMPTY_CONFIG, EMPTY_CONFIG, EMPTY_CONFIG, EMPTY_CONFIG, EMPTY_CONFIG, EMPTY_CONFIG,
]) {
  return {
    getConfig: vi.fn((type: string) => {
      const index = ['sonarr', 'radarr', 'lidarr', 'readarr', 'chaptarr', 'whisparr', 'sportarr', 'lazylibrarian'].indexOf(type);
      return of(configs[index] ?? EMPTY_CONFIG);
    }),
    updateConfig: vi.fn(() => of(undefined)),
    createInstance: vi.fn(() => of(SONARR_INSTANCE)),
    updateInstance: vi.fn(() => of(SONARR_INSTANCE)),
    deleteInstance: vi.fn(() => of(undefined)),
    testInstance: vi.fn(() => of({ message: 'Connected' })),
  };
}

function createToast() {
  return { success: vi.fn(), error: vi.fn() };
}

interface Setup {
  fixture: ComponentFixture<ArrAppsComponent>;
  component: ArrAppsComponent;
  api: ReturnType<typeof createApi>;
  toast: ReturnType<typeof createToast>;
  confirm: ConfirmService;
}

describe('ArrAppsComponent', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  function setup(api = createApi(), toast = createToast()): Setup {
    TestBed.configureTestingModule({
      providers: [
        { provide: ArrApi, useValue: api },
        { provide: ToastService, useValue: toast },
        { provide: FeatureBadgeService, useValue: { isNew: (id: string) => id === 'chaptarr' } },
        { provide: ThemeService, useValue: { themedIconSrc: (src: string) => src } },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { fragment: null } },
        },
      ],
    });

    const fixture = TestBed.createComponent(ArrAppsComponent);
    fixture.detectChanges();

    return {
      fixture,
      component: fixture.componentInstance,
      api,
      toast,
      confirm: TestBed.inject(ConfirmService),
    };
  }

  it('loads all arr types and separates configured from unconfigured', () => {
    const { component, api } = setup();

    expect(api.getConfig).toHaveBeenCalledTimes(8);
    expect(component.configured()).toHaveLength(2);
    expect(component.unconfigured()).toHaveLength(6);
    expect(component.configured().map(e => e.type)).toEqual(['sonarr', 'radarr']);
    expect(component.unconfigured()).toEqual(['lidarr', 'readarr', 'chaptarr', 'whisparr', 'sportarr', 'lazylibrarian']);
  });

  it('renders configured apps as arr-section components', () => {
    const { fixture } = setup();

    const sections = fixture.nativeElement.querySelectorAll('app-arr-section');
    expect(sections.length).toBe(2);
  });

  it('renders unconfigured apps as selection cards in add strip', () => {
    const { fixture } = setup();

    const addCard = fixture.nativeElement.querySelector('.list-header__title');
    expect(addCard.textContent).toContain('Add another app');

    const cards = fixture.nativeElement.querySelectorAll('.selection-card');
    expect(cards.length).toBe(6);
  });

  it('treats a config missing the instances field as unconfigured', () => {
    const configWithoutInstances = { failedImportMaxStrikes: -1 } as ArrConfig;
    const api = createApi([
      configWithoutInstances, RADARR_CONFIG, EMPTY_CONFIG, EMPTY_CONFIG, EMPTY_CONFIG, EMPTY_CONFIG, EMPTY_CONFIG, EMPTY_CONFIG,
    ]);
    const { component } = setup(api);

    expect(component.configured().map((e) => e.type)).toEqual(['radarr']);
    expect(component.unconfigured()).toContain('sonarr');
  });

  it('shows the NEW badge on the chaptarr selection card only', () => {
    const { fixture } = setup();

    const cards = Array.from(fixture.nativeElement.querySelectorAll('.selection-card')) as HTMLElement[];
    const chaptarrCard = cards.find((el) => el.textContent!.includes('Chaptarr'))!;
    const otherCards = cards.filter((el) => el !== chaptarrCard);

    expect(chaptarrCard.querySelector('app-new-badge .new-ribbon')).not.toBeNull();
    otherCards.forEach((card) => expect(card.querySelector('.new-ribbon')).toBeNull());
  });

  it('shows the experimental badge on the lazylibrarian selection card only', () => {
    const { fixture } = setup();

    const cards = Array.from(fixture.nativeElement.querySelectorAll('.selection-card')) as HTMLElement[];
    const lazyLibrarianCard = cards.find((el) => el.textContent!.includes('LazyLibrarian'))!;
    const otherCards = cards.filter((el) => el !== lazyLibrarianCard);

    expect(lazyLibrarianCard.querySelector('app-badge')).not.toBeNull();
    expect(lazyLibrarianCard.textContent).toContain('Experimental');
    otherCards.forEach((card) => expect(card.querySelector('app-badge')).toBeNull());
  });

  it('scrolls to the fragment from the route once settings load', async () => {
    const scrollIntoView = vi.fn();
    (Element.prototype as unknown as { scrollIntoView: typeof scrollIntoView }).scrollIntoView = scrollIntoView;

    try {
      TestBed.configureTestingModule({
        providers: [
          { provide: ArrApi, useValue: createApi() },
          { provide: ToastService, useValue: createToast() },
          { provide: ThemeService, useValue: { themedIconSrc: (src: string) => src } },
          { provide: ActivatedRoute, useValue: { snapshot: { fragment: 'radarr' } } },
        ],
      });

      const fixture = TestBed.createComponent(ArrAppsComponent);
      fixture.detectChanges();
      await Promise.resolve();

      expect(scrollIntoView).toHaveBeenCalledWith({ block: 'start' });
    } finally {
      delete (Element.prototype as { scrollIntoView?: unknown }).scrollIntoView;
    }
  });

  it('shows the loading state while the initial config request is pending', () => {
    vi.useFakeTimers();
    try {
      const pending = new Subject<ArrConfig>();
      const api = createApi();
      api.getConfig.mockReturnValue(pending);

      TestBed.configureTestingModule({
        providers: [
          { provide: ArrApi, useValue: api },
          { provide: ToastService, useValue: createToast() },
          { provide: ThemeService, useValue: { themedIconSrc: (src: string) => src } },
          { provide: ActivatedRoute, useValue: { snapshot: { fragment: null } } },
        ],
      });

      const fixture = TestBed.createComponent(ArrAppsComponent);
      fixture.detectChanges();

      expect(fixture.componentInstance.configured()).toEqual([]);
      expect(fixture.componentInstance.unconfigured()).toEqual([]);

      vi.advanceTimersByTime(200);
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('app-loading-state')).not.toBeNull();

      pending.next(EMPTY_CONFIG);
      pending.complete();
    } finally {
      vi.useRealTimers();
    }
  });

  it('retries the load when the Retry button is clicked', () => {
    const api = createApi();
    api.getConfig.mockReturnValue(throwError(() => new Error('offline')));
    const { fixture } = setup(api);

    api.getConfig.mockReturnValue(of(EMPTY_CONFIG));
    const retryButton = Array.from(fixture.nativeElement.querySelectorAll('button'))
      .find((el) => (el as HTMLElement).textContent!.trim() === 'Retry') as HTMLButtonElement;
    retryButton.click();
    fixture.detectChanges();

    expect(fixture.componentInstance.loadError()).toBe(false);
  });

  it('hides the "Add another app" card when every app is configured', () => {
    const api = createApi([
      SONARR_CONFIG, RADARR_CONFIG, SONARR_CONFIG, SONARR_CONFIG, SONARR_CONFIG, SONARR_CONFIG, SONARR_CONFIG, SONARR_CONFIG,
    ]);
    const { fixture } = setup(api);

    expect(fixture.nativeElement.querySelector('.list-header__title')).toBeNull();
  });

  it('shows empty state when no apps are configured', () => {
    const api = createApi([
      EMPTY_CONFIG, EMPTY_CONFIG, EMPTY_CONFIG, EMPTY_CONFIG, EMPTY_CONFIG, EMPTY_CONFIG, EMPTY_CONFIG, EMPTY_CONFIG,
    ]);
    const { fixture: f } = setup(api);

    expect(f.nativeElement.textContent).toContain('No media managers yet');
    expect(f.nativeElement.textContent).toContain('Pick an app below to connect it.');
  });

  it('opens add modal for selected unconfigured app type', () => {
    const { fixture, component } = setup();

    component.openAddModal('lidarr');
    fixture.detectChanges();

    expect(component.modalVisible()).toBe(true);
    expect(component.modalType()).toBe('lidarr');
    expect(component.editingInstance()).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Add Lidarr Instance');
  });

  it('sets version to first option when opening add modal', () => {
    const { component } = setup();

    component.openAddModal('whisparr');

    expect(component.instanceModel().version).toBe(2);
  });

  it('creates an instance and reloads the page', () => {
    const { fixture, component, api, toast } = setup();

    component.openAddModal('sonarr');
    component.instanceForm.name().value.set('New Sonarr');
    component.instanceForm.url().value.set('http://localhost:8989');
    component.instanceForm.apiKey().value.set('key');
    fixture.detectChanges();

    component.saveInstance();
    fixture.detectChanges();

    expect(api.createInstance).toHaveBeenCalledWith('sonarr', {
      name: 'New Sonarr',
      url: 'http://localhost:8989',
      externalUrl: undefined,
      apiKey: 'key',
      version: 4,
      enabled: true,
    });
    expect(toast.success).toHaveBeenCalledWith('Instance added');
    expect(component.modalVisible()).toBe(false);
  });

  it('opens edit modal for a selected instance', () => {
    const { fixture, component } = setup();

    component.openEditModal('sonarr', SONARR_INSTANCE);
    fixture.detectChanges();

    expect(component.modalVisible()).toBe(true);
    expect(component.modalType()).toBe('sonarr');
    expect(component.editingInstance()).toBe(SONARR_INSTANCE);
    expect(fixture.nativeElement.textContent).toContain('Edit Sonarr Instance');
    expect(component.instanceModel().name).toBe('Main Sonarr');
  });

  it('defaults externalUrl to an empty string when the instance has none', () => {
    const { component } = setup();

    component.openEditModal('radarr', RADARR_INSTANCE);

    expect(component.instanceModel().externalUrl).toBe('');
  });

  it('keeps a non-default version when editing after another type', () => {
    const { fixture, component } = setup();
    const whisparrV3: ArrInstance = { ...SONARR_INSTANCE, id: 'whisparr-1', version: 3 };

    component.openEditModal('sonarr', SONARR_INSTANCE);
    fixture.detectChanges();
    component.modalVisible.set(false);
    component.openEditModal('whisparr', whisparrV3);
    fixture.detectChanges();

    expect(component.instanceModel().version).toBe(3);
  });

  it('updates an instance by id', () => {
    const { fixture, component, api, toast } = setup();

    component.openEditModal('sonarr', SONARR_INSTANCE);
    component.instanceForm.name().value.set('Renamed');
    fixture.detectChanges();

    component.saveInstance();
    fixture.detectChanges();

    expect(api.updateInstance).toHaveBeenCalledWith('sonarr', 'sonarr-1', {
      name: 'Renamed',
      url: 'http://localhost:8989',
      externalUrl: 'https://sonarr.example.com',
      apiKey: 'sonarr-key',
      version: 4,
      enabled: true,
    });
    expect(toast.success).toHaveBeenCalledWith('Instance updated');
  });

  it('tests connection with instance id when editing', () => {
    const { fixture, component, api } = setup();

    component.openEditModal('sonarr', SONARR_INSTANCE);
    fixture.detectChanges();

    component.testConnection();
    fixture.detectChanges();

    expect(api.testInstance).toHaveBeenCalledWith('sonarr', {
      url: 'http://localhost:8989',
      apiKey: 'sonarr-key',
      version: 4,
      instanceId: 'sonarr-1',
    });
  });

  it('falls back to a default success message when the API returns none', () => {
    const api = createApi();
    api.testInstance.mockReturnValue(of({ message: '' }));
    const { fixture, component, toast } = setup(api);

    component.openAddModal('sonarr');
    fixture.detectChanges();

    component.testConnection();
    fixture.detectChanges();

    expect(toast.success).toHaveBeenCalledWith('Connection successful');
  });

  it('tests connection without instance id when adding', () => {
    const { fixture, component, api } = setup();

    component.openAddModal('radarr');
    fixture.detectChanges();

    component.testConnection();
    fixture.detectChanges();

    expect(api.testInstance).toHaveBeenCalledWith('radarr', {
      url: '',
      apiKey: '',
      version: 6,
      instanceId: undefined,
    });
  });

  it('does not call the API when deleting an instance without an id', async () => {
    const { component, api } = setup();
    const instanceWithoutId: ArrInstance = { ...SONARR_INSTANCE, id: '' };

    await component.deleteInstance('sonarr', instanceWithoutId);

    expect(api.deleteInstance).not.toHaveBeenCalled();
  });

  it('deletes an instance after confirmation', async () => {
    const { fixture, component, api, toast, confirm } = setup();

    const cancelled = component.deleteInstance('sonarr', SONARR_INSTANCE);
    confirm.cancel();
    await cancelled;

    expect(api.deleteInstance).not.toHaveBeenCalled();

    const accepted = component.deleteInstance('sonarr', SONARR_INSTANCE);
    confirm.accept();
    await accepted;
    fixture.detectChanges();

    expect(api.deleteInstance).toHaveBeenCalledWith('sonarr', 'sonarr-1');
    expect(toast.success).toHaveBeenCalledWith('Instance deleted');
  });

  it('reports pending changes while modal is edited', () => {
    const { fixture, component } = setup();

    expect(component.hasPendingChanges()).toBe(false);

    component.openAddModal('sonarr');
    fixture.detectChanges();
    expect(component.hasPendingChanges()).toBe(false);

    component.instanceForm.name().value.set('New');
    fixture.detectChanges();
    expect(component.hasPendingChanges()).toBe(true);

    component.instanceForm.url().value.set('http://localhost:8989');
    component.instanceForm.apiKey().value.set('key');
    component.saveInstance();
    fixture.detectChanges();

    expect(component.hasPendingChanges()).toBe(false);
  });

  it('keeps an arr-section instance mounted and its unsaved edits while a delete reload is in flight', async () => {
    const api = createApi();
    const { fixture, component, confirm } = setup(api);

    const sectionBefore = component.sections()[0];
    const sectionElBefore = fixture.nativeElement.querySelectorAll('app-arr-section')[0];
    sectionBefore.failedImportForm.override().value.set(true);
    sectionBefore.failedImportForm.maxStrikes().value.set(9);
    fixture.detectChanges();

    const pending = new Subject<ArrConfig>();
    api.getConfig.mockReturnValue(pending);

    const deleted = component.deleteInstance('radarr', RADARR_INSTANCE);
    confirm.accept();
    await deleted;
    fixture.detectChanges();

    expect(component.loader.loading()).toBe(true);
    expect(fixture.nativeElement.querySelectorAll('app-arr-section')[0]).toBe(sectionElBefore);
    expect(component.sections()[0]).toBe(sectionBefore);

    pending.next(SONARR_CONFIG);
    pending.complete();
    fixture.detectChanges();

    const sectionAfter = component.sections()[0];
    expect(sectionAfter).toBe(sectionBefore);
    expect(sectionAfter.failedImportModel()).toEqual({ override: true, maxStrikes: 9 });
  });

  it('reports pending changes from arr-section components', () => {
    const { component } = setup();

    expect(component.hasPendingChanges()).toBe(false);

    const sections = component.sections();
    expect(sections.length).toBeGreaterThan(0);
    vi.spyOn(sections[0], 'hasPendingChanges').mockReturnValue(true);
    expect(component.hasPendingChanges()).toBe(true);
  });

  it('shows connection error state and recovers on retry', () => {
    const api = createApi();
    api.getConfig.mockReturnValue(throwError(() => new Error('offline')));
    const { fixture, component, toast } = setup(api);

    expect(component.loadError()).toBe(true);
    expect(toast.error).toHaveBeenCalledWith('Failed to load media manager settings: offline');
    expect(fixture.nativeElement.textContent).toContain('Could not connect to server');

    api.getConfig.mockReturnValue(of(EMPTY_CONFIG));
    component.retry();
    fixture.detectChanges();

    expect(component.loadError()).toBe(false);
  });

  it('keeps modal open and reports error when save fails', () => {
    const api = createApi();
    api.createInstance.mockReturnValue(throwError(() => ({ message: 'Name exists' })));
    const { fixture: f, component, toast } = setup(api);

    component.openAddModal('sonarr');
    component.instanceForm.name().value.set('New');
    component.instanceForm.url().value.set('http://localhost:8989');
    component.instanceForm.apiKey().value.set('key');
    f.detectChanges();

    component.saveInstance();
    f.detectChanges();

    expect(toast.error).toHaveBeenCalledWith('Name exists');
    expect(component.modalVisible()).toBe(true);
  });

  it('does not call the API when saving an invalid form', () => {
    const { component, api } = setup();

    component.openAddModal('sonarr');

    component.saveInstance();

    expect(api.createInstance).not.toHaveBeenCalled();
    expect(api.updateInstance).not.toHaveBeenCalled();
  });

  it('toasts the error when deleting an instance fails', async () => {
    const api = createApi();
    api.deleteInstance.mockReturnValue(throwError(() => ({ message: 'Delete failed' })));
    const { component, toast, confirm } = setup(api);

    const deleted = component.deleteInstance('sonarr', SONARR_INSTANCE);
    confirm.accept();
    await deleted;

    expect(toast.error).toHaveBeenCalledWith('Delete failed');
  });

  it('shows error when test connection fails', () => {
    const api = createApi();
    api.testInstance.mockReturnValue(throwError(() => ({ message: 'Connection refused' })));
    const { fixture: f, component, toast } = setup(api);

    component.openAddModal('sonarr');
    f.detectChanges();

    component.testConnection();
    f.detectChanges();

    expect(toast.error).toHaveBeenCalledWith('Connection refused');
    expect(component.testing()).toBe(false);
  });

  it('emits from arr-section when add is clicked', () => {
    const { component } = setup();

    const sections = component.sections();
    expect(sections.length).toBeGreaterThan(0);
    const openAddSpy = vi.spyOn(component, 'openAddModal');
    sections[0].onAddClick();
    expect(openAddSpy).toHaveBeenCalled();
  });

  it('emits from arr-section when edit is clicked', () => {
    const { component } = setup();

    const sections = component.sections();
    expect(sections.length).toBeGreaterThan(0);
    const openEditSpy = vi.spyOn(component, 'openEditModal');
    sections[0].onEditClick(SONARR_INSTANCE);
    expect(openEditSpy).toHaveBeenCalled();
  });

  it('emits from arr-section when delete is clicked', () => {
    const { component } = setup();

    const sections = component.sections();
    expect(sections.length).toBeGreaterThan(0);
    const deleteSpy = vi.spyOn(component, 'deleteInstance');
    sections[0].onDeleteClick(SONARR_INSTANCE);
    expect(deleteSpy).toHaveBeenCalled();
  });

  it('disables save button when form has errors', () => {
    const { fixture, component } = setup();

    component.openAddModal('sonarr');
    fixture.detectChanges();

    const saveButton = Array.from(fixture.nativeElement.querySelectorAll('[modal-footer] button'))
      .find((el) => (el as HTMLElement).textContent!.trim() === 'Save') as HTMLButtonElement | undefined;
    expect(saveButton).toBeDefined();
    expect(saveButton!.disabled).toBe(true);
  });

  it('validates required fields', () => {
    const { fixture, component } = setup();

    component.openAddModal('sonarr');
    fixture.detectChanges();

    expect(component.instanceForm.name().errors()[0]?.message).toBe('Name is required');
    expect(component.instanceForm.url().errors()[0]?.message).toBe('URL is required');
    expect(component.instanceForm.apiKey().errors()[0]?.message).toBe('API key is required');
  });
});
