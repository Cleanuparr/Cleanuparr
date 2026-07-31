import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { BlacklistSyncApi } from '@core/api/blacklist-sync.api';
import { ApiError } from '@core/interceptors/error.interceptor';
import { ToastService } from '@core/services/toast.service';
import { BlacklistSyncConfig } from '@shared/models/blacklist-sync-config.model';
import { BlacklistSyncComponent } from './blacklist-sync.component';

const CONFIG: BlacklistSyncConfig = {
  id: 'cfg-1',
  enabled: true,
  blacklistPath: 'https://example.com/blacklist.txt',
};

const CONFIG_WITHOUT_PATH: BlacklistSyncConfig = {
  id: 'cfg-2',
  enabled: false,
};

function createApi(config: BlacklistSyncConfig) {
  return {
    getConfig: vi.fn(() => of(config)),
    updateConfig: vi.fn(() => of(undefined)),
  };
}

interface Setup {
  fixture: ComponentFixture<BlacklistSyncComponent>;
  component: BlacklistSyncComponent;
  api: ReturnType<typeof createApi>;
  toast: ToastService;
}

describe('BlacklistSyncComponent', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    TestBed.inject(ToastService).clear();
  });

  function setup(config: BlacklistSyncConfig = CONFIG): Setup {
    const api = createApi(config);

    TestBed.configureTestingModule({
      providers: [provideHttpClient(), { provide: BlacklistSyncApi, useValue: api }],
    });

    const fixture = TestBed.createComponent(BlacklistSyncComponent);
    fixture.detectChanges();

    return {
      fixture,
      component: fixture.componentInstance,
      api,
      toast: TestBed.inject(ToastService),
    };
  }

  it('loads the config into the form and renders the path input while clean', () => {
    const { fixture, component } = setup();

    expect(component.bsForm.enabled().value()).toBe(true);
    expect(component.bsForm.blacklistPath().value()).toBe('https://example.com/blacklist.txt');
    expect(component.dirty()).toBe(false);
    expect(component.hasPendingChanges()).toBe(false);
    expect(fixture.nativeElement.textContent).toContain('Blacklist File Path');
  });

  it('defaults a missing path to an empty string and hides the input while disabled', () => {
    const { fixture, component } = setup(CONFIG_WITHOUT_PATH);

    expect(component.bsForm.enabled().value()).toBe(false);
    expect(component.bsForm.blacklistPath().value()).toBe('');
    expect(component.hasErrors()).toBe(false);
    expect(fixture.nativeElement.textContent).not.toContain('Blacklist File Path');
  });

  it('requires the path only while sync is enabled', () => {
    const { fixture, component } = setup(CONFIG_WITHOUT_PATH);

    component.bsForm.enabled().value.set(true);
    fixture.detectChanges();

    expect(component.bsForm.blacklistPath().errors()[0]?.message).toBe(
      'This field is required when blacklist sync is enabled',
    );
    expect(component.hasErrors()).toBe(true);

    component.bsForm.blacklistPath().value.set('/config/blacklist');
    fixture.detectChanges();

    expect(component.bsForm.blacklistPath().errors()).toEqual([]);
    expect(component.hasErrors()).toBe(false);
  });

  it('turns dirty after an edit, saves the loaded id and goes clean again', () => {
    const { fixture, component, api } = setup();

    component.bsForm.blacklistPath().value.set('/config/custom');
    fixture.detectChanges();

    expect(component.dirty()).toBe(true);

    component.save();
    fixture.detectChanges();

    expect(api.updateConfig).toHaveBeenCalledWith({
      id: 'cfg-1',
      enabled: true,
      blacklistPath: '/config/custom',
    });
    expect(component.dirty()).toBe(false);
    expect(component.saved()).toBe(true);
    expect(component.saving()).toBe(false);
  });

  it('sends an undefined path when the field is blank', () => {
    const { fixture, component, api } = setup(CONFIG_WITHOUT_PATH);

    component.save();
    fixture.detectChanges();

    expect(api.updateConfig).toHaveBeenCalledWith({
      id: 'cfg-2',
      enabled: false,
      blacklistPath: undefined,
    });
  });

  it('surfaces the server message on a bad request and stops the saving state', () => {
    const { fixture, component, api, toast } = setup();

    const error = new ApiError('Blacklist path is not reachable');
    error.statusCode = 400;
    api.updateConfig.mockReturnValue(throwError(() => error));

    component.save();
    fixture.detectChanges();

    expect(toast.toasts().at(-1)?.message).toBe('Blacklist path is not reachable');
    expect(component.saving()).toBe(false);
    expect(component.saved()).toBe(false);
  });

  it('falls back to a generic message for non-400 failures', () => {
    const { fixture, component, api, toast } = setup();

    const error = new ApiError('Internal Server Error');
    error.statusCode = 500;
    api.updateConfig.mockReturnValue(throwError(() => error));

    component.save();
    fixture.detectChanges();

    expect(toast.toasts().at(-1)?.message).toBe('Failed to save blacklist sync settings');
  });
});
