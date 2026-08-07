import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ArrApi } from '@core/api/arr.api';
import { ConfirmService } from '@core/services/confirm.service';
import { ToastService } from '@core/services/toast.service';
import { ArrConfig, ArrInstance } from '@shared/models/arr-config.model';
import { ArrSettingsComponent } from './arr-settings.component';

const INSTANCE: ArrInstance = {
  id: 'instance-1',
  enabled: true,
  name: 'Main Sonarr',
  url: 'http://localhost:8989',
  externalUrl: 'https://sonarr.example.com',
  apiKey: 'abc123',
  version: 4,
};

const DISABLED_INSTANCE: ArrInstance = {
  id: 'instance-2',
  enabled: false,
  name: 'Backup Sonarr',
  url: 'http://localhost:8990',
  apiKey: 'def456',
  version: 4,
};

const CONFIG: ArrConfig = {
  failedImportMaxStrikes: 3,
  instances: [INSTANCE, DISABLED_INSTANCE],
};

function createApi(config: ArrConfig = CONFIG) {
  return {
    getConfig: vi.fn(() => of(config)),
    createInstance: vi.fn(() => of(INSTANCE)),
    updateInstance: vi.fn(() => of(INSTANCE)),
    deleteInstance: vi.fn(() => of(undefined)),
    testInstance: vi.fn(() => of({ message: 'Connected to Sonarr v4' })),
  };
}

function createToast() {
  return { success: vi.fn(), error: vi.fn() };
}

interface Setup {
  fixture: ComponentFixture<ArrSettingsComponent>;
  component: ArrSettingsComponent;
  api: ReturnType<typeof createApi>;
  toast: ReturnType<typeof createToast>;
  confirm: ConfirmService;
}

function text(fixture: ComponentFixture<ArrSettingsComponent>, selector: string): string[] {
  return Array.from(fixture.nativeElement.querySelectorAll(selector)).map((el) =>
    (el as HTMLElement).textContent!.trim(),
  );
}

describe('ArrSettingsComponent', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  function setup(type = 'sonarr', api = createApi(), toast = createToast()): Setup {
    TestBed.configureTestingModule({
      providers: [
        { provide: ArrApi, useValue: api },
        { provide: ToastService, useValue: toast },
      ],
    });

    const fixture = TestBed.createComponent(ArrSettingsComponent);
    fixture.componentRef.setInput('type', type);
    fixture.detectChanges();

    return {
      fixture,
      component: fixture.componentInstance,
      api,
      toast,
      confirm: TestBed.inject(ConfirmService),
    };
  }

  it('loads the instances for the routed arr type and renders them', () => {
    const { fixture, component, api } = setup('sonarr');

    expect(api.getConfig).toHaveBeenCalledWith('sonarr');
    expect(component.displayName()).toBe('Sonarr');
    expect(component.instances()).toEqual([INSTANCE, DISABLED_INSTANCE]);
    expect(text(fixture, '.instance-row__name')).toEqual(['Main Sonarr', 'Backup Sonarr']);
    expect(text(fixture, '.instance-row__url')).toEqual([
      'http://localhost:8989',
      'http://localhost:8990',
    ]);
    expect(fixture.nativeElement.textContent).toContain('Sonarr Settings');
    expect(component.hasPendingChanges()).toBe(false);
  });

  it('shows an empty state when the arr type has no instances', () => {
    const { fixture, component } = setup('radarr', createApi({ failedImportMaxStrikes: 0, instances: [] }));

    expect(component.instances()).toEqual([]);
    expect(fixture.nativeElement.textContent).toContain('No instances configured');
    expect(fixture.nativeElement.textContent).toContain('Add a Radarr instance to get started.');
  });

  it('offers only the api versions supported by the arr type and defaults to the first one', () => {
    const { fixture, component } = setup('whisparr');

    expect(component.versionOptions()).toEqual([
      { label: 'v2', value: 2 },
      { label: 'v3', value: 3 },
    ]);
    expect(component.instanceModel().version).toBe(2);

    component.openAddModal();
    fixture.detectChanges();

    const trigger = fixture.nativeElement.querySelector('.select-trigger') as HTMLButtonElement;
    expect(trigger.textContent!.trim()).toBe('v2');

    trigger.click();
    fixture.detectChanges();

    expect(text(fixture, '.select-option')).toEqual(['v2', 'v3']);
  });

  it('exposes the single supported version for a single-version arr type', () => {
    const { component } = setup('sonarr');

    expect(component.versionOptions()).toEqual([{ label: 'v4', value: 4 }]);
    expect(component.instanceModel().version).toBe(4);
  });

  it('falls back to no version options and version 3 for an unknown arr type', () => {
    const { fixture, component } = setup('bogusarr');

    expect(component.versionOptions()).toEqual([]);
    expect(component.instanceModel().version).toBe(3);

    component.openAddModal();
    fixture.detectChanges();

    expect(component.instanceModel().version).toBe(3);
    expect(fixture.nativeElement.querySelectorAll('.select-option').length).toBe(0);
  });

  it('shows sonarr\'s own default port and example url as placeholders', () => {
    const { component } = setup('sonarr');
    expect(component.urlPlaceholder()).toBe('http://localhost:8989');
    expect(component.externalUrlPlaceholder()).toBe('https://sonarr.example.com');
  });

  it('shows sportarr\'s own default port and example url as placeholders, not sonarr\'s', () => {
    const { component } = setup('sportarr');
    expect(component.urlPlaceholder()).toBe('http://localhost:1867');
    expect(component.externalUrlPlaceholder()).toBe('https://sportarr.example.com');
  });

  it('falls back to the sonarr default port for an unknown arr type url placeholder', () => {
    const { component } = setup('bogusarr');
    expect(component.urlPlaceholder()).toBe('http://localhost:8989');
  });

  it('opens a blank add modal and blocks saving until name, url and api key are filled in', () => {
    const { fixture, component, api } = setup('sonarr');

    component.openAddModal();
    fixture.detectChanges();

    expect(component.editingInstance()).toBeNull();
    expect(component.modalVisible()).toBe(true);
    expect(component.instanceModel()).toEqual({
      name: '',
      url: '',
      externalUrl: '',
      apiKey: '',
      version: 4,
      enabled: true,
    });
    expect(fixture.nativeElement.textContent).toContain('Add Instance');
    expect(component.hasModalErrors()).toBe(true);
    expect(component.instanceForm.name().errors()[0].message).toBe('Name is required');
    expect(component.instanceForm.url().errors()[0].message).toBe('URL is required');
    expect(component.instanceForm.apiKey().errors()[0].message).toBe('API key is required');

    component.saveInstance();
    expect(api.createInstance).not.toHaveBeenCalled();
    expect(component.saving()).toBe(false);

    component.instanceForm.name().value.set('New');
    component.instanceForm.url().value.set('http://localhost:7878');
    component.instanceForm.apiKey().value.set('key');
    fixture.detectChanges();

    expect(component.hasModalErrors()).toBe(false);
  });

  it('creates an instance, omits a blank external url and reloads the list', () => {
    const { fixture, component, api, toast } = setup('sonarr');

    component.openAddModal();
    component.instanceForm.name().value.set('New Sonarr');
    component.instanceForm.url().value.set('http://localhost:8989');
    component.instanceForm.apiKey().value.set('key-1');
    fixture.detectChanges();

    component.saveInstance();
    fixture.detectChanges();

    expect(api.createInstance).toHaveBeenCalledWith('sonarr', {
      name: 'New Sonarr',
      url: 'http://localhost:8989',
      externalUrl: undefined,
      apiKey: 'key-1',
      version: 4,
      enabled: true,
    });
    expect(toast.success).toHaveBeenCalledWith('Instance added');
    expect(component.modalVisible()).toBe(false);
    expect(component.saving()).toBe(false);
    expect(api.getConfig).toHaveBeenCalledTimes(2);
  });

  it('loads an existing instance into the modal and updates it by id', () => {
    const { fixture, component, api, toast } = setup('sonarr');

    component.openEditModal(INSTANCE);
    fixture.detectChanges();

    expect(component.editingInstance()).toBe(INSTANCE);
    expect(component.instanceModel()).toEqual({
      name: 'Main Sonarr',
      url: 'http://localhost:8989',
      externalUrl: 'https://sonarr.example.com',
      apiKey: 'abc123',
      version: 4,
      enabled: true,
    });
    expect(fixture.nativeElement.textContent).toContain('Edit Instance');

    component.instanceForm.name().value.set('Renamed');
    fixture.detectChanges();
    component.saveInstance();
    fixture.detectChanges();

    expect(api.updateInstance).toHaveBeenCalledWith('sonarr', 'instance-1', {
      name: 'Renamed',
      url: 'http://localhost:8989',
      externalUrl: 'https://sonarr.example.com',
      apiKey: 'abc123',
      version: 4,
      enabled: true,
    });
    expect(api.createInstance).not.toHaveBeenCalled();
    expect(toast.success).toHaveBeenCalledWith('Instance updated');
  });

  it('keeps the modal open and reports the failure when saving fails', () => {
    const api = createApi();
    api.createInstance.mockReturnValue(throwError(() => new Error('boom')));
    const { fixture, component, toast } = setup('sonarr', api);

    component.openAddModal();
    component.instanceForm.name().value.set('New');
    component.instanceForm.url().value.set('http://localhost:8989');
    component.instanceForm.apiKey().value.set('key');
    fixture.detectChanges();

    component.saveInstance();
    fixture.detectChanges();

    expect(toast.error).toHaveBeenCalledWith('Failed to save instance');
    expect(component.modalVisible()).toBe(true);
    expect(component.saving()).toBe(false);
  });

  it('tests the connection for the edited instance and surfaces the server message', () => {
    const { fixture, component, api, toast } = setup('sonarr');

    component.openEditModal(INSTANCE);
    fixture.detectChanges();

    component.testConnection();
    fixture.detectChanges();

    expect(api.testInstance).toHaveBeenCalledWith('sonarr', {
      url: 'http://localhost:8989',
      apiKey: 'abc123',
      version: 4,
      instanceId: 'instance-1',
    });
    expect(toast.success).toHaveBeenCalledWith('Connected to Sonarr v4');
    expect(component.testing()).toBe(false);
  });

  it('reports a failed connection test and stops the testing spinner', () => {
    const api = createApi();
    api.testInstance.mockReturnValue(throwError(() => new Error('refused')));
    const { fixture, component, toast } = setup('sonarr', api);

    component.openAddModal();
    fixture.detectChanges();

    component.testConnection();
    fixture.detectChanges();

    expect(api.testInstance).toHaveBeenCalledWith('sonarr', {
      url: '',
      apiKey: '',
      version: 4,
      instanceId: undefined,
    });
    expect(toast.error).toHaveBeenCalledWith('Connection test failed');
    expect(component.testing()).toBe(false);
  });

  it('deletes an instance only after the destructive confirmation is accepted', async () => {
    const { fixture, component, api, toast, confirm } = setup('sonarr');

    const cancelled = component.deleteInstance(INSTANCE);
    expect(confirm.state()).toMatchObject({
      title: 'Delete Instance',
      confirmLabel: 'Delete',
      destructive: true,
    });
    expect(confirm.state()!.message).toContain('Main Sonarr');
    confirm.cancel();
    await cancelled;

    expect(api.deleteInstance).not.toHaveBeenCalled();

    const accepted = component.deleteInstance(INSTANCE);
    confirm.accept();
    await accepted;
    fixture.detectChanges();

    expect(api.deleteInstance).toHaveBeenCalledWith('sonarr', 'instance-1');
    expect(toast.success).toHaveBeenCalledWith('Instance deleted');
    expect(api.getConfig).toHaveBeenCalledTimes(2);
  });

  it('shows the connection error state when loading fails and recovers on retry', () => {
    const api = createApi();
    api.getConfig.mockReturnValue(throwError(() => new Error('offline')));
    const { fixture, component, toast } = setup('sonarr', api);

    expect(component.loadError()).toBe(true);
    expect(component.instances()).toEqual([]);
    expect(toast.error).toHaveBeenCalledWith('Failed to load Sonarr settings');
    expect(fixture.nativeElement.textContent).toContain('Could not connect to server');

    api.getConfig.mockReturnValue(of(CONFIG));
    component.retry();
    fixture.detectChanges();

    expect(component.loadError()).toBe(false);
    expect(text(fixture, '.instance-row__name')).toEqual(['Main Sonarr', 'Backup Sonarr']);
  });
});
