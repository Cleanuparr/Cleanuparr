import { Component, ChangeDetectionStrategy, inject, signal, computed, OnInit, viewChildren, effect, untracked } from '@angular/core';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import {
  CardComponent, ButtonComponent, InputComponent, ToggleComponent,
  NumberInputComponent, SelectComponent, ChipInputComponent, AccordionComponent,
  EmptyStateComponent, LoadingStateComponent, type SelectOption,
} from '@ui';
import { DownloadCleanerApi } from '@core/api/download-cleaner.api';
import { ApiError } from '@core/interceptors/error.interceptor';
import { ToastService } from '@core/services/toast.service';
import {
  DownloadCleanerConfig, CleanCategory, ClientCleanerConfig,
  createDefaultCategory, createDefaultUnlinkedConfig,
} from '@shared/models/download-cleaner-config.model';
import { ScheduleOptions } from '@shared/models/queue-cleaner-config.model';
import { ScheduleUnit, TorrentPrivacyType } from '@shared/models/enums';
import { HasPendingChanges } from '@core/guards/pending-changes.guard';
import { DeferredLoader } from '@shared/utils/loading.util';
import { generateCronExpression, parseCronToJobSchedule } from '@shared/utils/schedule.util';

const SCHEDULE_UNIT_OPTIONS: SelectOption[] = [
  { label: 'Seconds', value: ScheduleUnit.Seconds },
  { label: 'Minutes', value: ScheduleUnit.Minutes },
  { label: 'Hours', value: ScheduleUnit.Hours },
];

const PRIVACY_TYPE_OPTIONS: SelectOption[] = [
  { label: 'Public', value: TorrentPrivacyType.Public },
  { label: 'Private', value: TorrentPrivacyType.Private },
  { label: 'Both', value: TorrentPrivacyType.Both },
];

@Component({
  selector: 'app-download-cleaner',
  standalone: true,
  imports: [
    PageHeaderComponent, CardComponent, ButtonComponent, InputComponent,
    ToggleComponent, NumberInputComponent, SelectComponent, ChipInputComponent, AccordionComponent,
    EmptyStateComponent, LoadingStateComponent,
  ],
  templateUrl: './download-cleaner.component.html',
  styleUrl: './download-cleaner.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DownloadCleanerComponent implements OnInit, HasPendingChanges {
  private readonly api = inject(DownloadCleanerApi);
  private readonly toast = inject(ToastService);
  private readonly chipInputs = viewChildren(ChipInputComponent);

  private readonly savedSnapshot = signal('');

  readonly scheduleUnitOptions = SCHEDULE_UNIT_OPTIONS;
  readonly privacyTypeOptions = PRIVACY_TYPE_OPTIONS;
  readonly loader = new DeferredLoader();
  readonly loadError = signal(false);
  readonly saving = signal(false);
  readonly saved = signal(false);

  // Global settings
  readonly enabled = signal(false);
  readonly useAdvancedScheduling = signal(false);
  readonly cronExpression = signal('');
  readonly scheduleEvery = signal<unknown>(5);
  readonly scheduleUnit = signal<unknown>(ScheduleUnit.Minutes);
  readonly ignoredDownloads = signal<string[]>([]);

  // Per-client settings
  readonly clientConfigs = signal<ClientCleanerConfig[]>([]);
  readonly selectedClientId = signal<string | null>(null);

  readonly selectedClient = computed(() =>
    this.clientConfigs().find(c => c.downloadClientId === this.selectedClientId()) ?? null
  );

  readonly clientOptions = computed<SelectOption[]>(() =>
    this.clientConfigs().map(c => ({ label: c.downloadClientName, value: c.downloadClientId }))
  );

  readonly categoriesExpanded = signal(true);
  readonly unlinkedExpanded = signal(false);

  readonly scheduleIntervalOptions = computed(() => {
    const unit = this.scheduleUnit() as ScheduleUnit;
    const values = ScheduleOptions[unit] ?? [];
    return values.map(v => ({ label: `${v}`, value: v }));
  });

  constructor() {
    effect(() => {
      const unit = this.scheduleUnit();
      const options = ScheduleOptions[unit as ScheduleUnit] ?? [];
      const current = this.scheduleEvery();
      if (options.length > 0 && !options.includes(current as number)) {
        untracked(() => this.scheduleEvery.set(options[0]));
      }
    });
  }

  readonly scheduleEveryError = computed(() => {
    if (this.useAdvancedScheduling()) return undefined;
    const unit = this.scheduleUnit() as ScheduleUnit;
    const options = ScheduleOptions[unit] ?? [];
    if (!options.includes(this.scheduleEvery() as number)) return 'Please select a value';
    return undefined;
  });

  readonly cronError = computed(() => {
    if (this.useAdvancedScheduling() && !this.cronExpression().trim()) return 'Cron expression is required';
    return undefined;
  });

  unlinkedTargetCategoryError(): string | undefined {
    const client = this.selectedClient();
    if (client?.unlinkedConfig?.enabled && !client.unlinkedConfig.targetCategory?.trim()) return 'Target category is required';
    return undefined;
  }

  unlinkedCategoriesError(): string | undefined {
    const client = this.selectedClient();
    if (client?.unlinkedConfig?.enabled && (client.unlinkedConfig.categories?.length ?? 0) === 0) {
      return 'At least one category is required when unlinked download handling is enabled';
    }
    return undefined;
  }

  directoryMappingError(): string | undefined {
    const client = this.selectedClient();
    if (!client?.unlinkedConfig) return undefined;
    const src = client.unlinkedConfig.downloadDirectorySource;
    const tgt = client.unlinkedConfig.downloadDirectoryTarget;
    if ((src && !tgt) || (!src && tgt)) {
      return 'Both source and target directories must be set, or both must be empty';
    }
    return undefined;
  }

  categoryNameError(cat: CleanCategory): string | undefined {
    if (!cat.name?.trim()) return 'Name is required';
    return undefined;
  }

  categoryDisabledError(cat: CleanCategory): string | undefined {
    if (cat.maxRatio === -1 && cat.maxSeedTime === -1) {
      return 'Both max ratio and max seed time cannot be disabled at the same time';
    }
    return undefined;
  }

  readonly noFeaturesError = computed(() => {
    if (!this.enabled()) return undefined;
    const anyClientHasFeatures = this.clientConfigs().some(c =>
      (c.seedingRules?.length ?? 0) > 0 || c.unlinkedConfig?.enabled
    );
    if (!anyClientHasFeatures) {
      return 'At least one feature must be configured on at least one download client';
    }
    return undefined;
  });

  readonly hasErrors = computed(() => {
    if (this.noFeaturesError()) return true;
    if (this.scheduleEveryError()) return true;
    if (this.cronError()) return true;
    if (this.chipInputs().some(c => c.hasUncommittedInput())) return true;
    // Validate all clients
    for (const client of this.clientConfigs()) {
      if (client.unlinkedConfig?.enabled) {
        if (!client.unlinkedConfig.targetCategory?.trim()) return true;
        if ((client.unlinkedConfig.categories?.length ?? 0) === 0) return true;
        const src = client.unlinkedConfig.downloadDirectorySource;
        const tgt = client.unlinkedConfig.downloadDirectoryTarget;
        if ((src && !tgt) || (!src && tgt)) return true;
      }
      for (const cat of client.seedingRules ?? []) {
        if (this.categoryNameError(cat) || this.categoryDisabledError(cat)) return true;
      }
    }
    return false;
  });

  private config: DownloadCleanerConfig | null = null;

  ngOnInit(): void {
    this.loadConfig();
  }

  private loadConfig(): void {
    this.loader.start();
    this.api.getConfig().subscribe({
      next: (config) => {
        this.config = config;
        this.enabled.set(config.enabled);
        this.useAdvancedScheduling.set(config.useAdvancedScheduling);
        this.cronExpression.set(config.cronExpression);
        const parsed = parseCronToJobSchedule(config.cronExpression);
        if (parsed) {
          this.scheduleEvery.set(parsed.every);
          this.scheduleUnit.set(parsed.type);
        }
        this.ignoredDownloads.set(config.ignoredDownloads ?? []);
        this.clientConfigs.set((config.clients ?? []).map(c => ({
          ...c,
          seedingRules: c.seedingRules ?? [],
          unlinkedConfig: c.unlinkedConfig ?? createDefaultUnlinkedConfig(),
        })));
        if (config.clients?.length > 0) {
          this.selectedClientId.set(config.clients[0].downloadClientId);
        }
        this.loader.stop();
        this.savedSnapshot.set(this.buildSnapshot());
      },
      error: () => {
        this.toast.error('Failed to load download cleaner settings');
        this.loader.stop();
        this.loadError.set(true);
      },
    });
  }

  retry(): void {
    this.loadError.set(false);
    this.loadConfig();
  }

  addCategory(): void {
    this.updateSelectedClient(client => ({
      ...client,
      seedingRules: [...(client.seedingRules ?? []), createDefaultCategory()],
    }));
  }

  removeCategory(index: number): void {
    this.updateSelectedClient(client => ({
      ...client,
      seedingRules: (client.seedingRules ?? []).filter((_, i) => i !== index),
    }));
  }

  updateCategory(index: number, field: keyof CleanCategory, value: any): void {
    this.updateSelectedClient(client => {
      const updated = [...(client.seedingRules ?? [])];
      updated[index] = { ...updated[index], [field]: value };
      return { ...client, seedingRules: updated };
    });
  }

  updateUnlinkedField(field: string, value: any): void {
    this.updateSelectedClient(client => ({
      ...client,
      unlinkedConfig: {
        ...(client.unlinkedConfig ?? createDefaultUnlinkedConfig()),
        [field]: value,
      },
    }));
  }

  private updateSelectedClient(updater: (client: ClientCleanerConfig) => ClientCleanerConfig): void {
    const id = this.selectedClientId();
    if (!id) return;
    this.clientConfigs.update(configs =>
      configs.map(c => c.downloadClientId === id ? updater(c) : c)
    );
  }

  save(): void {
    if (!this.config) return;

    const jobSchedule = { every: (this.scheduleEvery() as number) ?? 5, type: this.scheduleUnit() as ScheduleUnit };
    const cronExpression = this.useAdvancedScheduling()
      ? this.cronExpression()
      : generateCronExpression(jobSchedule);

    const config: DownloadCleanerConfig = {
      enabled: this.enabled(),
      cronExpression,
      useAdvancedScheduling: this.useAdvancedScheduling(),
      ignoredDownloads: this.ignoredDownloads(),
      clients: this.clientConfigs(),
    };

    this.saving.set(true);
    this.api.updateConfig(config).subscribe({
      next: () => {
        this.toast.success('Download cleaner settings saved');
        this.saving.set(false);
        this.saved.set(true);
        setTimeout(() => this.saved.set(false), 1500);
        this.savedSnapshot.set(this.buildSnapshot());
      },
      error: (err: ApiError) => {
        this.toast.error(err.statusCode === 400
          ? err.message
          : 'Failed to save download cleaner settings');
        this.saving.set(false);
      },
    });
  }

  private buildSnapshot(): string {
    return JSON.stringify({
      enabled: this.enabled(),
      useAdvancedScheduling: this.useAdvancedScheduling(),
      cronExpression: this.cronExpression(),
      scheduleEvery: this.scheduleEvery(),
      scheduleUnit: this.scheduleUnit(),
      ignoredDownloads: this.ignoredDownloads(),
      clientConfigs: this.clientConfigs(),
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
