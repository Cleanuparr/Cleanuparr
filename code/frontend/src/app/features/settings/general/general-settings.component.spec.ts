import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { GeneralConfigApi } from '@core/api/general-config.api';
import { ConfirmService } from '@core/services/confirm.service';
import { ToastService } from '@core/services/toast.service';
import { GeneralConfig } from '@shared/models/general-config.model';
import { CertificateValidationType, LogEventLevel } from '@shared/models/enums';
import { GeneralSettingsComponent } from './general-settings.component';

const CONFIG: GeneralConfig = {
  displaySupportBanner: false,
  dryRun: true,
  httpMaxRetries: 2,
  httpTimeout: 45,
  httpCertificateValidation: CertificateValidationType.DisabledForLocalAddresses,
  statusCheckEnabled: false,
  strikeInactivityWindowHours: 12,
  historyRetentionDays: 90,
  ignoredDownloads: ['ignored-hash'],
  connectivityCheckEnabled: false,
  connectivityCheckUrls: [],
  auth: {
    disableAuthForLocalAddresses: true,
    trustForwardedHeaders: true,
    trustedNetworks: ['10.0.0.0/8'],
  },
  log: {
    level: LogEventLevel.Debug,
    rollingSizeMB: 20,
    retainedFileCount: 7,
    timeLimitHours: 48,
    archiveEnabled: true,
    archiveRetainedCount: 4,
    archiveTimeLimitHours: 240,
  },
};

const SPARSE_CONFIG = {
  displaySupportBanner: true,
  dryRun: false,
  httpMaxRetries: 3,
  httpTimeout: 30,
  httpCertificateValidation: CertificateValidationType.Enabled,
  statusCheckEnabled: true,
  strikeInactivityWindowHours: 24,
  historyRetentionDays: 365,
} as GeneralConfig;

function createApi(config: GeneralConfig) {
  return {
    get: vi.fn(() => of(config)),
    update: vi.fn(() => of(undefined)),
    purgeStrikes: vi.fn(() => of({ deletedStrikes: 12, deletedItems: 3 })),
  };
}

interface Setup {
  fixture: ComponentFixture<GeneralSettingsComponent>;
  component: GeneralSettingsComponent;
  api: ReturnType<typeof createApi>;
  confirm: ConfirmService;
  toast: ToastService;
}

describe('GeneralSettingsComponent', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    TestBed.inject(ToastService).clear();
    TestBed.inject(ConfirmService).state.set(null);
  });

  function setup(config: GeneralConfig = CONFIG): Setup {
    const api = createApi(config);

    TestBed.configureTestingModule({
      providers: [provideHttpClient(), { provide: GeneralConfigApi, useValue: api }],
    });

    const fixture = TestBed.createComponent(GeneralSettingsComponent);
    fixture.detectChanges();

    return {
      fixture,
      component: fixture.componentInstance,
      api,
      confirm: TestBed.inject(ConfirmService),
      toast: TestBed.inject(ToastService),
    };
  }

  function saveButton(fixture: ComponentFixture<GeneralSettingsComponent>): HTMLButtonElement {
    return fixture.nativeElement.querySelector('.form-actions button') as HTMLButtonElement;
  }

  it('keeps the save button disabled until an edit makes the form dirty', () => {
    const { fixture, component } = setup();

    expect(saveButton(fixture).disabled).toBe(true);

    component.genForm.httpTimeout().value.set(60);
    fixture.detectChanges();

    expect(component.dirty()).toBe(true);
    expect(saveButton(fixture).disabled).toBe(false);
  });

  it('flattens the nested auth and log config into the form and stays clean', () => {
    const { component } = setup();

    expect(component.genForm.dryRun().value()).toBe(true);
    expect(component.genForm.httpMaxRetries().value()).toBe(2);
    expect(component.genForm.httpTimeout().value()).toBe(45);
    expect(component.genForm.httpCertificateValidation().value()).toBe(
      CertificateValidationType.DisabledForLocalAddresses,
    );
    expect(component.genForm.ignoredDownloads().value()).toEqual(['ignored-hash']);
    expect(component.genForm.authDisableLocalAuth().value()).toBe(true);
    expect(component.genForm.authTrustedNetworks().value()).toEqual(['10.0.0.0/8']);
    expect(component.genForm.logLevel().value()).toBe(LogEventLevel.Debug);
    expect(component.genForm.logArchiveRetainedCount().value()).toBe(4);
    expect(component.dirty()).toBe(false);
    expect(component.hasPendingChanges()).toBe(false);
    expect(component.hasErrors()).toBe(false);
  });

  it('applies the logging and auth defaults when the server omits those sections', () => {
    const { component } = setup(SPARSE_CONFIG);

    expect(component.genForm.logLevel().value()).toBe(LogEventLevel.Information);
    expect(component.genForm.logRollingSizeMB().value()).toBe(10);
    expect(component.genForm.logRetainedFileCount().value()).toBe(5);
    expect(component.genForm.logTimeLimitHours().value()).toBe(168);
    expect(component.genForm.logArchiveEnabled().value()).toBe(false);
    expect(component.genForm.logArchiveRetainedCount().value()).toBe(3);
    expect(component.genForm.logArchiveTimeLimitHours().value()).toBe(720);
    expect(component.genForm.authDisableLocalAuth().value()).toBe(false);
    expect(component.genForm.authTrustForwardedHeaders().value()).toBe(false);
    expect(component.genForm.authTrustedNetworks().value()).toEqual([]);
    expect(component.genForm.ignoredDownloads().value()).toEqual([]);
    expect(component.genForm.connectivityCheckEnabled().value()).toBe(false);
    expect(component.genForm.connectivityCheckUrls().value()).toEqual([]);
    expect(component.dirty()).toBe(false);
  });

  it('turns dirty after an edit, rebuilds the nested payload on save and goes clean again', () => {
    const { fixture, component, api } = setup();

    component.genForm.httpTimeout().value.set(60);
    component.genForm.logLevel().value.set(LogEventLevel.Warning);
    fixture.detectChanges();

    expect(component.dirty()).toBe(true);

    component.save();
    fixture.detectChanges();

    expect(api.update).toHaveBeenCalledWith({
      displaySupportBanner: false,
      dryRun: true,
      httpMaxRetries: 2,
      httpTimeout: 60,
      httpCertificateValidation: CertificateValidationType.DisabledForLocalAddresses,
      statusCheckEnabled: false,
      strikeInactivityWindowHours: 12,
      historyRetentionDays: 90,
      ignoredDownloads: ['ignored-hash'],
      connectivityCheckEnabled: false,
      connectivityCheckUrls: [],
      auth: {
        disableAuthForLocalAddresses: true,
        trustForwardedHeaders: true,
        trustedNetworks: ['10.0.0.0/8'],
      },
      log: {
        level: LogEventLevel.Warning,
        rollingSizeMB: 20,
        retainedFileCount: 7,
        timeLimitHours: 48,
        archiveEnabled: true,
        archiveRetainedCount: 4,
        archiveTimeLimitHours: 240,
      },
    });
    expect(component.dirty()).toBe(false);
    expect(component.saved()).toBe(true);
    expect(component.saving()).toBe(false);
  });

  it('substitutes the defaults for every numeric field left empty', () => {
    const { fixture, component, api } = setup();

    component.genForm.httpMaxRetries().value.set(null);
    component.genForm.httpTimeout().value.set(null);
    component.genForm.strikeInactivityWindowHours().value.set(null);
    component.genForm.historyRetentionDays().value.set(null);
    component.genForm.logRollingSizeMB().value.set(null);
    component.genForm.logRetainedFileCount().value.set(null);
    component.genForm.logTimeLimitHours().value.set(null);
    component.genForm.logArchiveRetainedCount().value.set(null);
    component.genForm.logArchiveTimeLimitHours().value.set(null);
    fixture.detectChanges();

    expect(component.hasErrors()).toBe(true);

    component.save();

    expect(api.update).toHaveBeenCalledWith(
      expect.objectContaining({
        httpMaxRetries: 3,
        httpTimeout: 30,
        strikeInactivityWindowHours: 24,
        historyRetentionDays: 365,
        log: expect.objectContaining({
          rollingSizeMB: 10,
          retainedFileCount: 5,
          timeLimitHours: 168,
          archiveRetainedCount: 3,
          archiveTimeLimitHours: 720,
        }),
      }),
    );
  });

  it('enforces the numeric bounds on the http and retention fields', () => {
    const { fixture, component } = setup();

    component.genForm.httpMaxRetries().value.set(6);
    component.genForm.httpTimeout().value.set(4);
    component.genForm.strikeInactivityWindowHours().value.set(200);
    component.genForm.historyRetentionDays().value.set(0);
    fixture.detectChanges();

    expect(component.genForm.httpMaxRetries().errors()[0]?.message).toBe('Maximum value is 5');
    expect(component.genForm.httpTimeout().errors()[0]?.message).toBe('Minimum value is 5');
    expect(component.genForm.strikeInactivityWindowHours().errors()[0]?.message).toBe(
      'Maximum value is 168 hours (7 days)',
    );
    expect(component.genForm.historyRetentionDays().errors()[0]?.message).toBe('Minimum value is 1');
    expect(component.hasErrors()).toBe(true);

    component.genForm.httpMaxRetries().value.set(5);
    component.genForm.httpTimeout().value.set(5);
    component.genForm.strikeInactivityWindowHours().value.set(168);
    component.genForm.historyRetentionDays().value.set(1);
    fixture.detectChanges();

    expect(component.hasErrors()).toBe(false);
  });

  it('reveals the connectivity urls once the check is on and demands at least one', () => {
    const { fixture, component } = setup();

    expect(fixture.nativeElement.textContent).not.toContain('Connectivity Check URLs');

    component.genForm.connectivityCheckEnabled().value.set(true);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Connectivity Check URLs');
    expect(component.genForm.connectivityCheckUrls().errors()[0]?.message).toBe(
      'Add at least one URL when the connectivity check is enabled',
    );
    expect(component.hasErrors()).toBe(true);

    component.genForm.connectivityCheckUrls().value.set(['https://github.com']);
    fixture.detectChanges();

    expect(component.genForm.connectivityCheckUrls().errors()).toEqual([]);
    expect(component.hasErrors()).toBe(false);
  });

  it('rejects a zeroed archive retention on both fields while archiving is enabled', () => {
    const { fixture, component } = setup();

    component.genForm.logArchiveRetainedCount().value.set(0);
    component.genForm.logArchiveTimeLimitHours().value.set(0);
    fixture.detectChanges();

    const message = 'Retained count and time limit cannot both be 0 when archiving is enabled';
    expect(component.genForm.logArchiveRetainedCount().errors()[0]?.message).toBe(message);
    expect(component.genForm.logArchiveTimeLimitHours().errors()[0]?.message).toBe(message);
    expect(component.hasErrors()).toBe(true);

    component.genForm.logArchiveEnabled().value.set(false);
    fixture.detectChanges();

    expect(component.genForm.logArchiveRetainedCount().errors()).toEqual([]);
    expect(component.genForm.logArchiveTimeLimitHours().errors()).toEqual([]);
    expect(component.hasErrors()).toBe(false);
  });

  it('hides the trusted network fields unless local auth is disabled', () => {
    const { fixture, component } = setup();

    expect(fixture.nativeElement.textContent).toContain('Additional Trusted Networks');

    component.genForm.authDisableLocalAuth().value.set(false);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).not.toContain('Additional Trusted Networks');
    expect(fixture.nativeElement.textContent).not.toContain('Trust Forwarded Headers');
  });

  it('purges strikes only after the confirmation is accepted', async () => {
    const { component, api, confirm, toast } = setup();

    const cancelled = component.confirmPurgeStrikes();
    expect(confirm.state()?.title).toBe('Purge All Strikes');
    expect(confirm.state()?.destructive).toBe(true);
    confirm.cancel();
    await cancelled;

    expect(api.purgeStrikes).not.toHaveBeenCalled();

    const accepted = component.confirmPurgeStrikes();
    confirm.accept();
    await accepted;

    expect(api.purgeStrikes).toHaveBeenCalledTimes(1);
    expect(toast.toasts().at(-1)?.message).toBe('Purged 12 strikes');
    expect(component.purgingStrikes()).toBe(false);
  });

  it('reports a failed save without clearing the pending changes', () => {
    const { fixture, component, api, toast } = setup();

    api.update.mockReturnValue(throwError(() => new Error('boom')));
    component.genForm.dryRun().value.set(false);
    fixture.detectChanges();

    component.save();
    fixture.detectChanges();

    expect(toast.toasts().at(-1)?.message).toBe('Failed to save general settings');
    expect(component.saving()).toBe(false);
    expect(component.saved()).toBe(false);
    expect(component.dirty()).toBe(true);
  });
});
