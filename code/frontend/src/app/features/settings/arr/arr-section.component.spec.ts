import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Component, input } from '@angular/core';
import { of, throwError } from 'rxjs';
import { ArrApi } from '@core/api/arr.api';
import { ApiError } from '@core/interceptors/error.interceptor';
import { ToastService } from '@core/services/toast.service';
import { FeatureBadgeService } from '@core/feature-badges/feature-badge.service';
import { ThemeService } from '@core/services/theme.service';
import { ArrConfig, ArrInstance } from '@shared/models/arr-config.model';
import type { ArrType } from '@shared/models/enums';
import { ArrSectionComponent } from './arr-section.component';

const INSTANCE: ArrInstance = {
  id: 'instance-1',
  enabled: true,
  name: 'Main Sonarr',
  url: 'http://localhost:8989',
  externalUrl: 'https://sonarr.example.com',
  apiKey: 'abc123',
  version: 4,
};

const CONFIG: ArrConfig = {
  failedImportMaxStrikes: 3,
  instances: [INSTANCE],
};

function createApi() {
  return {
    updateConfig: vi.fn(() => of(undefined)),
  };
}

function createToast() {
  return { success: vi.fn(), error: vi.fn() };
}

function createTheme() {
  return {
    themedIconSrc: vi.fn((src: string) => src),
  };
}

interface Setup {
  fixture: ComponentFixture<HostComponent>;
  component: ArrSectionComponent;
  api: ReturnType<typeof createApi>;
  toast: ReturnType<typeof createToast>;
  theme: ReturnType<typeof createTheme>;
}

@Component({
  standalone: true,
  imports: [ArrSectionComponent],
  template: `
    <app-arr-section
      [type]="type()"
      [config]="config()"
      [reloading]="reloading()"
      (add)="onAdd()"
      (edit)="onEdit($event)"
      (delete)="onDelete($event)"
    />
  `,
})
class HostComponent {
  type = input.required<ArrType>();
  config = input.required<ArrConfig>();
  reloading = input(false);
  onAdd = vi.fn();
  onEdit = vi.fn();
  onDelete = vi.fn();
}

function text(fixture: ComponentFixture<HostComponent>, selector: string): string[] {
  return Array.from(fixture.nativeElement.querySelectorAll(selector)).map((el) =>
    (el as HTMLElement).textContent!.trim(),
  );
}

describe('ArrSectionComponent', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  function setup(
    type: ArrType = 'sonarr', config = CONFIG, api = createApi(), toast = createToast(), theme = createTheme(),
  ): Setup {
    TestBed.configureTestingModule({
      providers: [
        { provide: ArrApi, useValue: api },
        { provide: ToastService, useValue: toast },
        { provide: ThemeService, useValue: theme },
        { provide: FeatureBadgeService, useValue: { isNew: () => true } },
      ],
    });

    const fixture = TestBed.createComponent(HostComponent);
    fixture.componentRef.setInput('type', type);
    fixture.componentRef.setInput('config', config);
    fixture.detectChanges();

    return {
      fixture,
      component: fixture.debugElement.query((el) => el.name === 'app-arr-section').componentInstance,
      api,
      toast,
      theme,
    };
  }

  it('renders instances from the config input', () => {
    const { fixture, component } = setup('sonarr');

    expect(component.instances()).toEqual([INSTANCE]);
    expect(text(fixture, '.item-row__name')).toEqual(['Main Sonarr']);
    expect(text(fixture, '.item-row__url')).toEqual(['http://localhost:8989']);
    expect(component.hasPendingChanges()).toBe(false);
  });

  it('treats a config with no instances field as having none', () => {
    const configWithoutInstances = { failedImportMaxStrikes: -1 } as ArrConfig;
    const { component } = setup('sonarr', configWithoutInstances);

    expect(component.instances()).toEqual([]);
  });

  it('shows a disabled badge for a disabled instance', () => {
    const disabledInstance: ArrInstance = { ...INSTANCE, enabled: false };
    const { fixture } = setup('sonarr', { failedImportMaxStrikes: 3, instances: [disabledInstance] });

    expect(text(fixture, '.item-row__info app-badge')).toEqual(['Disabled']);
  });

  it('emits add output when Add button is clicked', () => {
    const { fixture, component } = setup('sonarr');
    const hostComponent = fixture.componentInstance;

    component.onAddClick();

    expect(hostComponent.onAdd).toHaveBeenCalled();
  });

  it('emits edit output when Edit button is clicked', () => {
    const { fixture, component } = setup('sonarr');

    component.onEditClick(INSTANCE);

    expect(fixture.componentInstance.onEdit).toHaveBeenCalledWith(INSTANCE);
  });

  it('emits delete output when Delete button is clicked', () => {
    const { fixture, component } = setup('sonarr');
    const hostComponent = fixture.componentInstance;

    component.onDeleteClick(INSTANCE);

    expect(hostComponent.onDelete).toHaveBeenCalledWith(INSTANCE);
  });

  it('shows the experimental badge only for an experimental arr type', () => {
    const { fixture } = setup('lazylibrarian', { ...CONFIG, instances: [] });

    expect(fixture.nativeElement.querySelector('.section-header__title app-badge')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Experimental');
  });

  it('shows the NEW ribbon for an arr type with a feature id', () => {
    const { fixture } = setup('chaptarr', { ...CONFIG, instances: [] });

    expect(fixture.nativeElement.querySelector('.new-ribbon')).not.toBeNull();
  });

  it('hides the NEW ribbon for an arr type without a feature id', () => {
    const { fixture } = setup('sonarr');

    expect(fixture.nativeElement.querySelector('.new-ribbon')).toBeNull();
  });

  it('hides the experimental badge for a non-experimental arr type', () => {
    const { fixture } = setup('sonarr');

    expect(fixture.nativeElement.querySelector('.section-header__title app-badge')).toBeNull();
  });

  it('themes the header icon through the theme service', () => {
    const { theme } = setup('sonarr');

    expect(theme.themedIconSrc).toHaveBeenCalledWith('icons/ext/sonarr-light.svg');
  });

  it('shows the max strikes input only while the override is on', () => {
    const { fixture, component } = setup('sonarr', { failedImportMaxStrikes: -1, instances: [] });
    component.strikesOpen.set(true);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('app-number-input')).toBeNull();

    component.failedImportForm.override().value.set(true);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('app-number-input')).not.toBeNull();

    component.failedImportForm.override().value.set(false);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('app-number-input')).toBeNull();
  });

  it('sets host id to the arr type', () => {
    const { fixture } = setup('radarr');

    const hostEl = fixture.nativeElement.querySelector('app-arr-section');
    expect(hostEl.id).toBe('radarr');
  });

  it('shows the strikes panel only when strikesOpen, dirty, or hasErrors is true', () => {
    const { fixture, component } = setup('sonarr');

    expect(fixture.nativeElement.querySelector('.strikes-panel')).toBeNull();
    expect(component.panelVisible()).toBe(false);

    component.strikesOpen.set(true);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.strikes-panel')).not.toBeNull();
    expect(component.panelVisible()).toBe(true);
  });

  it('reflects panelVisible on the strikes toggle button aria-expanded attribute', () => {
    const { fixture } = setup('sonarr');

    const strikesButton = Array.from(fixture.nativeElement.querySelectorAll('.section-header__actions button'))
      .find((el) => (el as HTMLElement).textContent!.includes('Strikes')) as HTMLButtonElement;
    expect(strikesButton.getAttribute('aria-expanded')).toBe('false');

    strikesButton.click();
    fixture.detectChanges();

    expect(strikesButton.getAttribute('aria-expanded')).toBe('true');
  });

  it('seeds the override toggle off with the config failedImportMaxStrikes of -1', () => {
    const { component } = setup('sonarr', { failedImportMaxStrikes: -1, instances: [] });

    expect(component.failedImportModel()).toEqual({ override: false, maxStrikes: 3 });
    expect(component.hasPendingChanges()).toBe(false);
  });

  it('seeds the override toggle on with the config failedImportMaxStrikes value', () => {
    const { component } = setup('sonarr', { failedImportMaxStrikes: 5, instances: [] });

    expect(component.failedImportModel()).toEqual({ override: true, maxStrikes: 5 });
    expect(component.hasPendingChanges()).toBe(false);
  });

  it('enables the override, sets a value and saves it', () => {
    const { fixture, component, api, toast } = setup('sonarr', { failedImportMaxStrikes: -1, instances: [] });

    component.failedImportForm.override().value.set(true);
    fixture.detectChanges();
    component.failedImportForm.maxStrikes().value.set(5);
    fixture.detectChanges();

    expect(component.hasPendingChanges()).toBe(true);

    component.saveFailedImport();
    fixture.detectChanges();

    expect(api.updateConfig).toHaveBeenCalledWith('sonarr', { failedImportMaxStrikes: 5 });
    expect(toast.success).toHaveBeenCalledWith('Failed import settings saved');
    expect(component.hasPendingChanges()).toBe(false);
  });

  it('saves via the Save Settings button click and shows the Saved! label', () => {
    const { fixture, component, api } = setup('sonarr', { failedImportMaxStrikes: -1, instances: [] });

    component.strikesOpen.set(true);
    component.failedImportForm.override().value.set(true);
    component.failedImportForm.maxStrikes().value.set(5);
    fixture.detectChanges();

    const saveButton = fixture.nativeElement.querySelector('.form-actions button') as HTMLButtonElement;
    saveButton.click();
    fixture.detectChanges();

    expect(api.updateConfig).toHaveBeenCalledWith('sonarr', { failedImportMaxStrikes: 5 });
    expect(saveButton.textContent!.trim()).toBe('Saved!');
  });

  it('disables Save Settings while the parent reloads', () => {
    const { fixture, component } = setup();

    component.strikesOpen.set(true);
    component.failedImportForm.maxStrikes().value.set(9);
    fixture.componentRef.setInput('reloading', true);
    fixture.detectChanges();

    const saveButton = fixture.nativeElement.querySelector('.form-actions button') as HTMLButtonElement;
    expect(component.hasPendingChanges()).toBe(true);
    expect(saveButton.disabled).toBe(true);

    fixture.componentRef.setInput('reloading', false);
    fixture.detectChanges();

    expect(saveButton.disabled).toBe(false);
  });

  it('disabling the override sends -1', () => {
    const { fixture, component, api } = setup('sonarr', { failedImportMaxStrikes: 5, instances: [] });

    component.failedImportForm.override().value.set(false);
    fixture.detectChanges();

    component.saveFailedImport();
    fixture.detectChanges();

    expect(api.updateConfig).toHaveBeenCalledWith('sonarr', { failedImportMaxStrikes: -1 });
  });

  it('rejects 1 and 2 strikes, accepts 0, and disables save while invalid', () => {
    const { fixture, component } = setup('sonarr', { failedImportMaxStrikes: -1, instances: [] });

    component.failedImportForm.override().value.set(true);
    fixture.detectChanges();

    component.failedImportForm.maxStrikes().value.set(1);
    fixture.detectChanges();
    expect(component.failedImportHasErrors()).toBe(true);
    expect(component.failedImportForm.maxStrikes().errors()[0].message).toBe('Minimum value is 3');

    component.failedImportForm.maxStrikes().value.set(2);
    fixture.detectChanges();
    expect(component.failedImportHasErrors()).toBe(true);

    component.failedImportForm.maxStrikes().value.set(0);
    fixture.detectChanges();
    expect(component.failedImportHasErrors()).toBe(false);

    component.failedImportForm.maxStrikes().value.set(1);
    fixture.detectChanges();
    const saveButton = fixture.nativeElement.querySelector('.form-actions button') as HTMLButtonElement;
    expect(saveButton.disabled).toBe(true);
  });

  it('rejects fractional failed import strikes', () => {
    const { fixture, component, api } = setup('sonarr', { failedImportMaxStrikes: -1, instances: [] });

    component.failedImportForm.override().value.set(true);
    fixture.detectChanges();

    component.failedImportForm.maxStrikes().value.set(3.5);
    fixture.detectChanges();

    expect(component.failedImportForm.maxStrikes().errors()[0].message).toBe('Value must be a whole number');
    expect(component.failedImportHasErrors()).toBe(true);

    component.saveFailedImport();
    fixture.detectChanges();

    expect(api.updateConfig).not.toHaveBeenCalled();
  });

  it.each([
    [null, 'This field is required'],
    [-5, 'Value cannot be negative'],
    [5001, 'Value cannot exceed 5000'],
  ])('rejects a failed import maxStrikes of %s with "%s"', (maxStrikes, message) => {
    const { fixture, component } = setup('sonarr', { failedImportMaxStrikes: -1, instances: [] });

    component.failedImportForm.override().value.set(true);
    fixture.detectChanges();

    component.failedImportForm.maxStrikes().value.set(maxStrikes);
    fixture.detectChanges();

    expect(component.failedImportForm.maxStrikes().errors()[0].message).toBe(message);
  });

  it('accepts a failed import maxStrikes of 5000', () => {
    const { fixture, component } = setup('sonarr', { failedImportMaxStrikes: -1, instances: [] });

    component.failedImportForm.override().value.set(true);
    fixture.detectChanges();

    component.failedImportForm.maxStrikes().value.set(5000);
    fixture.detectChanges();

    expect(component.failedImportForm.maxStrikes().errors().length).toBe(0);
  });

  it('ignores an invalid failed import maxStrikes while the override is off', () => {
    const { fixture, component } = setup('sonarr', { failedImportMaxStrikes: -1, instances: [] });

    component.failedImportForm.maxStrikes().value.set(1);
    fixture.detectChanges();

    expect(component.failedImportForm.maxStrikes().errors().length).toBe(0);
    expect(component.failedImportHasErrors()).toBe(false);
  });

  it('stays clean when the override is toggled back off after editing the hidden value', () => {
    const { fixture, component } = setup('sonarr', { failedImportMaxStrikes: -1, instances: [] });

    component.failedImportForm.override().value.set(true);
    fixture.detectChanges();
    component.failedImportForm.maxStrikes().value.set(9999);
    fixture.detectChanges();
    component.failedImportForm.override().value.set(false);
    fixture.detectChanges();

    expect(component.hasPendingChanges()).toBe(false);
    expect(component.failedImportHasErrors()).toBe(false);
  });

  it('toasts the error when saving the failed import override fails', () => {
    const api = createApi();
    api.updateConfig.mockReturnValue(throwError(() => new ApiError('Save failed')));
    const toast = createToast();
    const { fixture, component } = setup('sonarr', { failedImportMaxStrikes: -1, instances: [] }, api, toast);

    component.failedImportForm.override().value.set(true);
    fixture.detectChanges();

    component.saveFailedImport();
    fixture.detectChanges();

    expect(toast.error).toHaveBeenCalledWith('Save failed');
    expect(component.failedImportSaving()).toBe(false);
  });

  it('keeps unsaved failed import edits when the config input changes with same type', () => {
    const api = createApi();
    const { fixture, component } = setup('sonarr', CONFIG, api);

    component.failedImportForm.maxStrikes().value.set(9);
    fixture.detectChanges();

    fixture.componentRef.setInput('config', { ...CONFIG, failedImportMaxStrikes: 7 });
    fixture.detectChanges();

    expect(component.failedImportModel().maxStrikes).toBe(9);
    expect(component.hasPendingChanges()).toBe(true);
  });

  it('reseeds the failed import settings when the type input changes despite unsaved edits', () => {
    const { fixture, component } = setup('sonarr');

    component.failedImportForm.maxStrikes().value.set(9);
    fixture.detectChanges();

    fixture.componentRef.setInput('type', 'radarr' as ArrType);
    fixture.componentRef.setInput('config', { ...CONFIG, failedImportMaxStrikes: -1 });
    fixture.detectChanges();

    expect(component.failedImportModel()).toEqual({ override: false, maxStrikes: 3 });
    expect(component.hasPendingChanges()).toBe(false);
  });
});
