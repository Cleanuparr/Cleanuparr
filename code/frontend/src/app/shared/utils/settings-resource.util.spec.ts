import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { NEVER, Observable, of, Subject, throwError } from 'rxjs';
import { ToastService } from '@core/services/toast.service';
import { ApiError } from '@core/interceptors/error.interceptor';
import { SAVED_FLASH_MS } from './dirty-tracker.util';
import { createSettingsResource, saveSettings, SettingsResource } from './settings-resource.util';

function createToast() {
  return { success: vi.fn(), error: vi.fn() };
}

describe('createSettingsResource', () => {
  function setup(load: () => Observable<number>) {
    const toast = createToast();
    TestBed.configureTestingModule({
      providers: [{ provide: ToastService, useValue: toast }],
    });

    let settingsResource!: SettingsResource<number>;
    TestBed.runInInjectionContext(() => {
      settingsResource = createSettingsResource<number, void>({ load, errorMessage: 'Failed to load' });
    });
    TestBed.tick();

    return { toast, settingsResource };
  }

  it('starts the loader while the request stays pending', () => {
    const { settingsResource } = setup(() => NEVER);

    expect(settingsResource.loader.loading()).toBe(true);
  });

  it('stops the loader once the source emits', async () => {
    const source = new Subject<number>();
    const { settingsResource } = setup(() => source);

    expect(settingsResource.loader.loading()).toBe(true);

    source.next(42);
    source.complete();
    await Promise.resolve();
    TestBed.tick();

    expect(settingsResource.loader.loading()).toBe(false);
    expect(settingsResource.resource.value()).toBe(42);
  });

  it('stops the loader and toasts the error message when the source errors', () => {
    const { settingsResource, toast } = setup(() => throwError(() => new Error('boom')));
    TestBed.tick();

    expect(settingsResource.loader.loading()).toBe(false);
    expect(settingsResource.loadError()).toBe(true);
    expect(toast.error).toHaveBeenCalledWith('Failed to load: boom');
  });

  it('evaluates a function error message lazily at error time', () => {
    const errorMessage = vi.fn(() => 'Dynamic failure');
    const toast = createToast();
    TestBed.configureTestingModule({
      providers: [{ provide: ToastService, useValue: toast }],
    });

    TestBed.runInInjectionContext(() => {
      createSettingsResource<number, void>({ load: () => throwError(() => new Error('boom')), errorMessage });
    });
    TestBed.tick();

    expect(errorMessage).toHaveBeenCalled();
    expect(toast.error).toHaveBeenCalledWith('Dynamic failure: boom');
  });
});

describe('saveSettings', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  function run(request: Observable<unknown>) {
    const toast = createToast();
    const saving = signal(false);
    const saved = signal(false);
    const onSaved = vi.fn();
    saveSettings({
      request,
      toast: toast as unknown as ToastService,
      saving,
      saved,
      successMessage: 'Settings saved',
      onSaved,
    });
    return { toast, saving, saved, onSaved };
  }

  it('toasts success, flashes saved and runs onSaved when the request succeeds', () => {
    vi.useFakeTimers();
    const { toast, saving, saved, onSaved } = run(of(undefined));

    expect(toast.success).toHaveBeenCalledWith('Settings saved');
    expect(saving()).toBe(false);
    expect(saved()).toBe(true);
    expect(onSaved).toHaveBeenCalled();

    vi.advanceTimersByTime(SAVED_FLASH_MS);
    expect(saved()).toBe(false);
  });

  it('toasts the backend message and clears saving when the request fails', () => {
    const { toast, saving, saved, onSaved } = run(throwError(() => new ApiError('boom')));

    expect(toast.error).toHaveBeenCalledWith('boom');
    expect(saving()).toBe(false);
    expect(saved()).toBe(false);
    expect(onSaved).not.toHaveBeenCalled();
  });

  it('flags saving while the request is pending', () => {
    const { saving } = run(NEVER);

    expect(saving()).toBe(true);
  });
});
