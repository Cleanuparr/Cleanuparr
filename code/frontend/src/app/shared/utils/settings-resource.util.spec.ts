import { TestBed } from '@angular/core/testing';
import { NEVER, Observable, Subject, throwError } from 'rxjs';
import { ToastService } from '@core/services/toast.service';
import { createSettingsResource, SettingsResource } from './settings-resource.util';

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
    expect(toast.error).toHaveBeenCalledWith('Failed to load');
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
    expect(toast.error).toHaveBeenCalledWith('Dynamic failure');
  });
});
