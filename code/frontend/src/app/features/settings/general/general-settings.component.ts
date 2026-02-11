import { Component, ChangeDetectionStrategy, inject, signal, computed, OnInit, viewChildren } from '@angular/core';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import {
  CardComponent, ButtonComponent, ToggleComponent,
  NumberInputComponent, SelectComponent, ChipInputComponent, AccordionComponent,
  EmptyStateComponent,
  type SelectOption,
} from '@ui';
import { GeneralConfigApi } from '@core/api/general-config.api';
import { ToastService } from '@core/services/toast.service';
import { GeneralConfig, LoggingConfig } from '@shared/models/general-config.model';
import { CertificateValidationType, LogEventLevel } from '@shared/models/enums';
import { HasPendingChanges } from '@core/guards/pending-changes.guard';

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

@Component({
  selector: 'app-general-settings',
  standalone: true,
  imports: [
    PageHeaderComponent, CardComponent, ButtonComponent,
    ToggleComponent, NumberInputComponent, SelectComponent, ChipInputComponent,
    AccordionComponent, EmptyStateComponent,
  ],
  templateUrl: './general-settings.component.html',
  styleUrl: './general-settings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GeneralSettingsComponent implements OnInit, HasPendingChanges {
  private readonly api = inject(GeneralConfigApi);
  private readonly toast = inject(ToastService);
  private readonly chipInputs = viewChildren(ChipInputComponent);

  private readonly savedSnapshot = signal('');

  readonly certOptions = CERT_OPTIONS;
  readonly logLevelOptions = LOG_LEVEL_OPTIONS;
  readonly loading = signal(false);
  readonly loadError = signal(false);
  readonly saving = signal(false);
  readonly saved = signal(false);

  // Form state
  readonly displaySupportBanner = signal(true);
  readonly dryRun = signal(false);
  readonly httpMaxRetries = signal<number | null>(3);
  readonly httpTimeout = signal<number | null>(30);
  readonly httpCertificateValidation = signal<unknown>(CertificateValidationType.Enabled);
  readonly searchEnabled = signal(true);
  readonly searchDelay = signal<number | null>(5);
  readonly ignoredDownloads = signal<string[]>([]);

  // Logging
  readonly logLevel = signal<unknown>(LogEventLevel.Information);
  readonly logRollingSizeMB = signal<number | null>(10);
  readonly logRetainedFileCount = signal<number | null>(5);
  readonly logTimeLimitHours = signal<number | null>(168);
  readonly logArchiveEnabled = signal(false);
  readonly logArchiveRetainedCount = signal<number | null>(3);
  readonly logArchiveTimeLimitHours = signal<number | null>(720);
  readonly logExpanded = signal(false);

  readonly httpMaxRetriesError = computed(() => {
    const v = this.httpMaxRetries();
    if (v == null) return 'This field is required';
    if (v < 0) return 'Minimum value is 0';
    if (v > 5) return 'Maximum value is 5';
    return undefined;
  });

  readonly httpTimeoutError = computed(() => {
    const v = this.httpTimeout();
    if (v == null) return 'This field is required';
    if (v < 1) return 'Minimum value is 1';
    if (v > 100) return 'Maximum value is 100';
    return undefined;
  });

  readonly searchDelayError = computed(() => {
    const v = this.searchDelay();
    if (v == null) return 'This field is required';
    if (v < 60) return 'Minimum value is 60';
    if (v > 300) return 'Maximum value is 300';
    return undefined;
  });

  readonly logRollingSizeError = computed(() => {
    const v = this.logRollingSizeMB();
    if (v == null) return 'This field is required';
    if (v < 1) return 'Minimum value is 1';
    if (v > 100) return 'Maximum value is 100 MB';
    return undefined;
  });

  readonly logRetainedFileCountError = computed(() => {
    const v = this.logRetainedFileCount();
    if (v == null) return 'This field is required';
    if (v < 0) return 'Minimum value is 0';
    if (v > 50) return 'Maximum value is 50';
    return undefined;
  });

  readonly logTimeLimitError = computed(() => {
    const v = this.logTimeLimitHours();
    if (v == null) return 'This field is required';
    if (v < 1) return 'Minimum value is 1';
    if (v > 1440) return 'Maximum value is 1440 hours (60 days)';
    return undefined;
  });

  readonly logArchiveRetainedError = computed(() => {
    const v = this.logArchiveRetainedCount();
    if (v == null) return 'This field is required';
    if (v < 0) return 'Minimum value is 0';
    if (v > 100) return 'Maximum value is 100';
    return undefined;
  });

  readonly logArchiveTimeLimitError = computed(() => {
    const v = this.logArchiveTimeLimitHours();
    if (v == null) return 'This field is required';
    if (v < 1) return 'Minimum value is 1';
    if (v > 1440) return 'Maximum value is 1440 hours (60 days)';
    return undefined;
  });

  readonly hasErrors = computed(() => !!(
    this.httpMaxRetriesError() ||
    this.httpTimeoutError() ||
    this.searchDelayError() ||
    this.logRollingSizeError() ||
    this.logRetainedFileCountError() ||
    this.logTimeLimitError() ||
    this.logArchiveRetainedError() ||
    this.logArchiveTimeLimitError() ||
    this.chipInputs().some(c => c.hasUncommittedInput())
  ));

  ngOnInit(): void {
    this.loadConfig();
  }

  private loadConfig(): void {
    this.loading.set(true);
    this.api.get().subscribe({
      next: (config) => {
        this.displaySupportBanner.set(config.displaySupportBanner);
        this.dryRun.set(config.dryRun);
        this.httpMaxRetries.set(config.httpMaxRetries);
        this.httpTimeout.set(config.httpTimeout);
        this.httpCertificateValidation.set(config.httpCertificateValidation);
        this.searchEnabled.set(config.searchEnabled);
        this.searchDelay.set(config.searchDelay);
        this.ignoredDownloads.set(config.ignoredDownloads ?? []);
        if (config.log) {
          this.logLevel.set(config.log.level);
          this.logRollingSizeMB.set(config.log.rollingSizeMB);
          this.logRetainedFileCount.set(config.log.retainedFileCount);
          this.logTimeLimitHours.set(config.log.timeLimitHours);
          this.logArchiveEnabled.set(config.log.archiveEnabled);
          this.logArchiveRetainedCount.set(config.log.archiveRetainedCount);
          this.logArchiveTimeLimitHours.set(config.log.archiveTimeLimitHours);
        }
        this.loading.set(false);
        this.savedSnapshot.set(this.buildSnapshot());
      },
      error: () => {
        this.toast.error('Failed to load general settings');
        this.loading.set(false);
        this.loadError.set(true);
      },
    });
  }

  retry(): void {
    this.loadError.set(false);
    this.loadConfig();
  }

  save(): void {
    const config: GeneralConfig = {
      displaySupportBanner: this.displaySupportBanner(),
      dryRun: this.dryRun(),
      httpMaxRetries: this.httpMaxRetries() ?? 3,
      httpTimeout: this.httpTimeout() ?? 30,
      httpCertificateValidation: this.httpCertificateValidation() as CertificateValidationType,
      searchEnabled: this.searchEnabled(),
      searchDelay: this.searchDelay() ?? 5,
      ignoredDownloads: this.ignoredDownloads(),
      log: {
        level: this.logLevel() as LogEventLevel,
        rollingSizeMB: this.logRollingSizeMB() ?? 10,
        retainedFileCount: this.logRetainedFileCount() ?? 5,
        timeLimitHours: this.logTimeLimitHours() ?? 168,
        archiveEnabled: this.logArchiveEnabled(),
        archiveRetainedCount: this.logArchiveRetainedCount() ?? 3,
        archiveTimeLimitHours: this.logArchiveTimeLimitHours() ?? 720,
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
    return JSON.stringify({
      displaySupportBanner: this.displaySupportBanner(),
      dryRun: this.dryRun(),
      httpMaxRetries: this.httpMaxRetries(),
      httpTimeout: this.httpTimeout(),
      httpCertificateValidation: this.httpCertificateValidation(),
      searchEnabled: this.searchEnabled(),
      searchDelay: this.searchDelay(),
      ignoredDownloads: this.ignoredDownloads(),
      logLevel: this.logLevel(),
      logRollingSizeMB: this.logRollingSizeMB(),
      logRetainedFileCount: this.logRetainedFileCount(),
      logTimeLimitHours: this.logTimeLimitHours(),
      logArchiveEnabled: this.logArchiveEnabled(),
      logArchiveRetainedCount: this.logArchiveRetainedCount(),
      logArchiveTimeLimitHours: this.logArchiveTimeLimitHours(),
    });
  }

  readonly dirty = computed(() => {
    const saved = this.savedSnapshot();
    return saved !== '' && saved !== this.buildSnapshot();
  });

  hasPendingChanges(): boolean {
    return this.dirty();
  }
}
