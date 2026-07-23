import { Component, ChangeDetectionStrategy, inject, signal, computed, effect, untracked, viewChildren } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { form, required, min, max, validate, FormField } from '@angular/forms/signals';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import {
  CardComponent, ButtonComponent, ToggleComponent,
  NumberInputComponent, SelectComponent, ChipInputComponent, AccordionComponent,
  EmptyStateComponent, LoadingStateComponent,
  type SelectOption,
} from '@ui';
import { GeneralConfigApi } from '@core/api/general-config.api';
import { ToastService } from '@core/services/toast.service';
import { ConfirmService } from '@core/services/confirm.service';
import { GeneralConfig } from '@shared/models/general-config.model';
import { CertificateValidationType, LogEventLevel } from '@shared/models/enums';
import { HasPendingChanges } from '@core/guards/pending-changes.guard';
import { DeferredLoader } from '@shared/utils/loading.util';

const CERT_OPTIONS: SelectOption[] = [
  { label: 'Enabled', value: CertificateValidationType.Enabled },
  { label: 'Disabled for Local', value: CertificateValidationType.DisabledForLocalAddresses },
  { label: 'Disabled', value: CertificateValidationType.Disabled },
];

const LOG_LEVEL_OPTIONS: SelectOption[] = [
  { label: 'Verbose', value: LogEventLevel.Verbose },
  { label: 'Debug', value: LogEventLevel.Debug },
  { label: 'Information', value: LogEventLevel.Information },
  { label: 'Warning', value: LogEventLevel.Warning },
  { label: 'Error', value: LogEventLevel.Error },
  { label: 'Fatal', value: LogEventLevel.Fatal },
];

interface GeneralSettingsFormModel {
  displaySupportBanner: boolean;
  dryRun: boolean;
  httpMaxRetries: number | null;
  httpTimeout: number | null;
  httpCertificateValidation: CertificateValidationType;
  statusCheckEnabled: boolean;
  ignoredDownloads: string[];
  connectivityCheckEnabled: boolean;
  connectivityCheckUrls: string[];
  strikeInactivityWindowHours: number | null;
  historyRetentionDays: number | null;
  authDisableLocalAuth: boolean;
  authTrustForwardedHeaders: boolean;
  authTrustedNetworks: string[];
  logLevel: LogEventLevel;
  logRollingSizeMB: number | null;
  logRetainedFileCount: number | null;
  logTimeLimitHours: number | null;
  logArchiveEnabled: boolean;
  logArchiveRetainedCount: number | null;
  logArchiveTimeLimitHours: number | null;
}

@Component({
  selector: 'app-general-settings',
  standalone: true,
  imports: [
    PageHeaderComponent, CardComponent, ButtonComponent,
    ToggleComponent, NumberInputComponent, SelectComponent, ChipInputComponent,
    AccordionComponent, EmptyStateComponent, LoadingStateComponent, FormField,
  ],
  templateUrl: './general-settings.component.html',
  styleUrl: './general-settings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GeneralSettingsComponent implements HasPendingChanges {
  private readonly api = inject(GeneralConfigApi);
  private readonly toast = inject(ToastService);
  private readonly confirmService = inject(ConfirmService);
  private readonly chipInputs = viewChildren(ChipInputComponent);

  private readonly savedSnapshot = signal('');

  private readonly configResource = rxResource({
    stream: () => this.api.get(),
  });

  readonly certOptions = CERT_OPTIONS;
  readonly logLevelOptions = LOG_LEVEL_OPTIONS;
  readonly loader = new DeferredLoader();
  readonly loadError = computed(() => !!this.configResource.error());
  readonly saving = signal(false);
  readonly saved = signal(false);

  // UI-only state (not part of the form model)
  readonly purgingStrikes = signal(false);
  readonly logExpanded = signal(false);

  private readonly model = signal<GeneralSettingsFormModel>({
    displaySupportBanner: true,
    dryRun: false,
    httpMaxRetries: 3,
    httpTimeout: 30,
    httpCertificateValidation: CertificateValidationType.Enabled,
    statusCheckEnabled: true,
    ignoredDownloads: [],
    connectivityCheckEnabled: false,
    connectivityCheckUrls: [],
    strikeInactivityWindowHours: 24,
    historyRetentionDays: 365,
    authDisableLocalAuth: false,
    authTrustForwardedHeaders: false,
    authTrustedNetworks: [],
    logLevel: LogEventLevel.Information,
    logRollingSizeMB: 10,
    logRetainedFileCount: 5,
    logTimeLimitHours: 168,
    logArchiveEnabled: false,
    logArchiveRetainedCount: 3,
    logArchiveTimeLimitHours: 720,
  });

  readonly genForm = form(this.model, (p) => {
    required(p.httpMaxRetries, { message: 'This field is required' });
    min(p.httpMaxRetries, 0, { message: 'Minimum value is 0' });
    max(p.httpMaxRetries, 5, { message: 'Maximum value is 5' });

    required(p.httpTimeout, { message: 'This field is required' });
    min(p.httpTimeout, 5, { message: 'Minimum value is 5' });
    max(p.httpTimeout, 100, { message: 'Maximum value is 100' });

    required(p.strikeInactivityWindowHours, { message: 'This field is required' });
    min(p.strikeInactivityWindowHours, 1, { message: 'Minimum value is 1' });
    max(p.strikeInactivityWindowHours, 168, { message: 'Maximum value is 168 hours (7 days)' });

    required(p.historyRetentionDays, { message: 'This field is required' });
    min(p.historyRetentionDays, 1, { message: 'Minimum value is 1' });
    max(p.historyRetentionDays, 3650, { message: 'Maximum value is 3650 days (10 years)' });

    required(p.logRollingSizeMB, { message: 'This field is required' });
    min(p.logRollingSizeMB, 0, { message: 'Minimum value is 0' });
    max(p.logRollingSizeMB, 100, { message: 'Maximum value is 100 MB' });

    required(p.logRetainedFileCount, { message: 'This field is required' });
    min(p.logRetainedFileCount, 0, { message: 'Minimum value is 0' });
    max(p.logRetainedFileCount, 50, { message: 'Maximum value is 50' });

    required(p.logTimeLimitHours, { message: 'This field is required' });
    min(p.logTimeLimitHours, 0, { message: 'Minimum value is 0' });
    max(p.logTimeLimitHours, 1440, { message: 'Maximum value is 1440 hours (60 days)' });

    required(p.logArchiveRetainedCount, { message: 'This field is required' });
    min(p.logArchiveRetainedCount, 0, { message: 'Minimum value is 0' });
    max(p.logArchiveRetainedCount, 100, { message: 'Maximum value is 100' });
    validate(p.logArchiveRetainedCount, () => this.bothZeroError());

    required(p.logArchiveTimeLimitHours, { message: 'This field is required' });
    min(p.logArchiveTimeLimitHours, 0, { message: 'Minimum value is 0' });
    max(p.logArchiveTimeLimitHours, 1440, { message: 'Maximum value is 1440 hours (60 days)' });
    validate(p.logArchiveTimeLimitHours, () => this.bothZeroError());
  });

  private bothZeroError() {
    const m = this.model();
    return m.logArchiveEnabled && m.logArchiveRetainedCount === 0 && m.logArchiveTimeLimitHours === 0
      ? { kind: 'bothZero', message: 'Retained count and time limit cannot both be 0 when archiving is enabled' }
      : undefined;
  }

  readonly hasErrors = computed(() =>
    this.genForm().invalid() || this.chipInputs().some(c => c.hasUncommittedInput())
  );

  constructor() {
    effect(() => {
      const config = this.configResource.hasValue() ? this.configResource.value() : undefined;
      if (!config) {
        return;
      }
      untracked(() => {
        this.model.set({
          displaySupportBanner: config.displaySupportBanner,
          dryRun: config.dryRun,
          httpMaxRetries: config.httpMaxRetries,
          httpTimeout: config.httpTimeout,
          httpCertificateValidation: config.httpCertificateValidation,
          statusCheckEnabled: config.statusCheckEnabled,
          ignoredDownloads: config.ignoredDownloads ?? [],
          connectivityCheckEnabled: config.connectivityCheckEnabled ?? false,
          connectivityCheckUrls: config.connectivityCheckUrls ?? [],
          strikeInactivityWindowHours: config.strikeInactivityWindowHours,
          historyRetentionDays: config.historyRetentionDays,
          authDisableLocalAuth: config.auth?.disableAuthForLocalAddresses ?? false,
          authTrustForwardedHeaders: config.auth?.trustForwardedHeaders ?? false,
          authTrustedNetworks: config.auth?.trustedNetworks ?? [],
          logLevel: config.log?.level ?? LogEventLevel.Information,
          logRollingSizeMB: config.log?.rollingSizeMB ?? 10,
          logRetainedFileCount: config.log?.retainedFileCount ?? 5,
          logTimeLimitHours: config.log?.timeLimitHours ?? 168,
          logArchiveEnabled: config.log?.archiveEnabled ?? false,
          logArchiveRetainedCount: config.log?.archiveRetainedCount ?? 3,
          logArchiveTimeLimitHours: config.log?.archiveTimeLimitHours ?? 720,
        });
        this.savedSnapshot.set(this.buildSnapshot());
      });
    });

    effect(() => {
      if (this.configResource.error()) {
        this.toast.error('Failed to load general settings');
      }
    });

    effect(() => {
      if (this.configResource.isLoading()) {
        this.loader.start();
      } else {
        this.loader.stop();
      }
    });
  }

  retry(): void {
    this.configResource.reload();
  }

  save(): void {
    const m = this.model();
    const config: GeneralConfig = {
      displaySupportBanner: m.displaySupportBanner,
      dryRun: m.dryRun,
      httpMaxRetries: m.httpMaxRetries ?? 3,
      httpTimeout: m.httpTimeout ?? 30,
      httpCertificateValidation: m.httpCertificateValidation as CertificateValidationType,
      statusCheckEnabled: m.statusCheckEnabled,
      strikeInactivityWindowHours: m.strikeInactivityWindowHours ?? 24,
      historyRetentionDays: m.historyRetentionDays ?? 365,
      ignoredDownloads: m.ignoredDownloads,
      connectivityCheckEnabled: m.connectivityCheckEnabled,
      connectivityCheckUrls: m.connectivityCheckUrls,
      auth: {
        disableAuthForLocalAddresses: m.authDisableLocalAuth,
        trustForwardedHeaders: m.authTrustForwardedHeaders,
        trustedNetworks: m.authTrustedNetworks,
      },
      log: {
        level: m.logLevel as LogEventLevel,
        rollingSizeMB: m.logRollingSizeMB ?? 10,
        retainedFileCount: m.logRetainedFileCount ?? 5,
        timeLimitHours: m.logTimeLimitHours ?? 168,
        archiveEnabled: m.logArchiveEnabled,
        archiveRetainedCount: m.logArchiveRetainedCount ?? 3,
        archiveTimeLimitHours: m.logArchiveTimeLimitHours ?? 720,
      },
    };

    this.saving.set(true);
    this.api.update(config).subscribe({
      next: () => {
        this.toast.success('General settings saved');
        this.saving.set(false);
        this.saved.set(true);
        setTimeout(() => this.saved.set(false), 1500);
        this.savedSnapshot.set(this.buildSnapshot());
      },
      error: () => {
        this.toast.error('Failed to save general settings');
        this.saving.set(false);
      },
    });
  }

  private buildSnapshot(): string {
    return JSON.stringify(this.model());
  }

  readonly dirty = computed(() => {
    const saved = this.savedSnapshot();
    return saved !== '' && saved !== this.buildSnapshot();
  });

  hasPendingChanges(): boolean {
    return this.dirty();
  }

  async confirmPurgeStrikes(): Promise<void> {
    const confirmed = await this.confirmService.confirm({
      title: 'Purge All Strikes',
      message: 'This will permanently delete all strike data for all downloads. Strike counts will reset to zero. This action cannot be undone.',
      confirmLabel: 'Purge',
      destructive: true,
    });
    if (confirmed) {
      this.purgeStrikes();
    }
  }

  private purgeStrikes(): void {
    this.purgingStrikes.set(true);
    this.api.purgeStrikes().subscribe({
      next: (result) => {
        this.toast.success(`Purged ${result.deletedStrikes} strikes`);
        this.purgingStrikes.set(false);
      },
      error: () => {
        this.toast.error('Failed to purge strikes');
        this.purgingStrikes.set(false);
      },
    });
  }
}
