import { Component, ChangeDetectionStrategy, inject, signal, computed, OnInit, viewChildren, effect, untracked } from '@angular/core';
import { NgIconComponent } from '@ng-icons/core';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import {
  CardComponent, ButtonComponent, InputComponent, ToggleComponent,
  NumberInputComponent, SelectComponent, ChipInputComponent, AccordionComponent,
  EmptyStateComponent, LoadingStateComponent, ModalComponent, BadgeComponent,
  type SelectOption,
} from '@ui';
import { DownloadCleanerApi } from '@core/api/download-cleaner.api';
import { ApiError } from '@core/interceptors/error.interceptor';
import { ToastService } from '@core/services/toast.service';
import { ConfirmService } from '@core/services/confirm.service';
import {
  DownloadCleanerConfig, SeedingRule, ClientCleanerConfig,
  createDefaultUnlinkedConfig,
} from '@shared/models/download-cleaner-config.model';
import { ScheduleOptions } from '@shared/models/queue-cleaner-config.model';
import { ScheduleUnit, TorrentPrivacyType, DownloadClientTypeName } from '@shared/models/enums';
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
    NgIconComponent,
    PageHeaderComponent, CardComponent, ButtonComponent, InputComponent,
    ToggleComponent, NumberInputComponent, SelectComponent, ChipInputComponent, AccordionComponent,
    EmptyStateComponent, LoadingStateComponent, ModalComponent, BadgeComponent,
  ],
  templateUrl: './download-cleaner.component.html',
  styleUrl: './download-cleaner.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DownloadCleanerComponent implements OnInit, HasPendingChanges {
  private readonly api = inject(DownloadCleanerApi);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmService);
  private readonly chipInputs = viewChildren(ChipInputComponent);

  private readonly savedSnapshot = signal('');

  readonly scheduleUnitOptions = SCHEDULE_UNIT_OPTIONS;
  readonly privacyTypeOptions = PRIVACY_TYPE_OPTIONS;
  readonly loader = new DeferredLoader();
  readonly loadError = signal(false);
  readonly saving = signal(false);
  readonly saved = signal(false);
  readonly unlinkedSaving = signal(false);
  readonly unlinkedSaved = signal(false);
  private readonly unlinkedSnapshots = signal<Record<string, string>>({});

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
    this.clientConfigs()
      .map(c => ({ label: c.downloadClientName, value: c.downloadClientId }))
      .sort((a, b) => a.label.localeCompare(b.label))
  );

  readonly isSelectedClientDisabled = computed(() =>
    this.selectedClient()?.downloadClientEnabled === false
  );

  readonly isSelectedClientQBittorrent = computed(() =>
    this.selectedClient()?.downloadClientTypeName === DownloadClientTypeName.qBittorrent
  );

  readonly seedingRulesExpanded = signal(false);
  readonly unlinkedExpanded = signal(false);

  // Seeding rule modal
  readonly ruleModalVisible = signal(false);
  readonly editingRule = signal<SeedingRule | null>(null);
  readonly ruleName = signal('');
  readonly rulePrivacyType = signal<unknown>(TorrentPrivacyType.Public);
  readonly ruleMaxRatio = signal<number | null>(-1);
  readonly ruleMinSeedTime = signal<number | null>(0);
  readonly ruleMaxSeedTime = signal<number | null>(-1);
  readonly ruleDeleteSourceFiles = signal(true);

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

  readonly ruleNameError = computed(() => {
    if (!this.ruleName().trim()) return 'Name is required';
    return undefined;
  });

  readonly ruleDisabledError = computed(() => {
    if ((this.ruleMaxRatio() ?? -1) < 0 && (this.ruleMaxSeedTime() ?? -1) < 0) {
      return 'Both max ratio and max seed time cannot be disabled at the same time';
    }
    return undefined;
  });

  readonly unlinkedCategoriesError = computed(() => {
    const client = this.selectedClient();
    if (!client?.unlinkedConfig?.enabled) return undefined;
    if ((client.unlinkedConfig.categories ?? []).length === 0) {
      return 'At least one category is required';
    }
    return undefined;
  });

  readonly unlinkedDirty = computed(() => {
    const client = this.selectedClient();
    if (!client) return false;
    const snapshots = this.unlinkedSnapshots();
    const saved = snapshots[client.downloadClientId];
    if (!saved) return false;
    return saved !== JSON.stringify(client.unlinkedConfig);
  });

  readonly hasGlobalErrors = computed(() => {
    if (this.scheduleEveryError()) return true;
    if (this.cronError()) return true;
    if (this.chipInputs().some(c => c.hasUncommittedInput())) return true;
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
        // Save unlinked config snapshots per client
        const snapshots: Record<string, string> = {};
        for (const c of config.clients ?? []) {
          snapshots[c.downloadClientId] = JSON.stringify(c.unlinkedConfig ?? createDefaultUnlinkedConfig());
        }
        this.unlinkedSnapshots.set(snapshots);

        this.loader.stop();
        // Defer snapshot so constructor effects (e.g. schedule unit clamping) settle first
        queueMicrotask(() => this.savedSnapshot.set(this.buildSnapshot()));
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

  // --- Seeding rule modal CRUD ---

  openRuleModal(rule?: SeedingRule): void {
    this.editingRule.set(rule ?? null);
    if (rule) {
      this.ruleName.set(rule.name);
      this.rulePrivacyType.set(rule.privacyType);
      this.ruleMaxRatio.set(rule.maxRatio);
      this.ruleMinSeedTime.set(rule.minSeedTime);
      this.ruleMaxSeedTime.set(rule.maxSeedTime);
      this.ruleDeleteSourceFiles.set(rule.deleteSourceFiles);
    } else {
      this.ruleName.set('');
      this.rulePrivacyType.set(TorrentPrivacyType.Public);
      this.ruleMaxRatio.set(-1);
      this.ruleMinSeedTime.set(0);
      this.ruleMaxSeedTime.set(-1);
      this.ruleDeleteSourceFiles.set(true);
    }
    this.ruleModalVisible.set(true);
  }

  saveRule(): void {
    if (this.ruleNameError() || this.ruleDisabledError()) return;
    const clientId = this.selectedClientId();
    if (!clientId) return;

    const dto: Partial<SeedingRule> = {
      name: this.ruleName().trim(),
      privacyType: this.rulePrivacyType() as TorrentPrivacyType,
      maxRatio: this.ruleMaxRatio() ?? -1,
      minSeedTime: this.ruleMinSeedTime() ?? 0,
      maxSeedTime: this.ruleMaxSeedTime() ?? -1,
      deleteSourceFiles: this.ruleDeleteSourceFiles(),
    };

    const editing = this.editingRule();
    const request = editing?.id
      ? this.api.updateSeedingRule(editing.id, dto)
      : this.api.createSeedingRule(clientId, dto);

    request.subscribe({
      next: () => {
        this.toast.success(editing ? 'Seeding rule updated' : 'Seeding rule created');
        this.ruleModalVisible.set(false);
        this.reloadSeedingRules(clientId);
      },
      error: (e: ApiError) => this.toast.error(e.statusCode === 400 ? e.message : 'Failed to save seeding rule'),
    });
  }

  async deleteRule(rule: SeedingRule): Promise<void> {
    const confirmed = await this.confirm.confirm({
      title: 'Delete Seeding Rule',
      message: `Are you sure you want to delete "${rule.name}"?`,
      confirmLabel: 'Delete',
      destructive: true,
    });
    if (!confirmed || !rule.id) return;
    const clientId = this.selectedClientId();
    if (!clientId) return;

    this.api.deleteSeedingRule(rule.id).subscribe({
      next: () => {
        this.toast.success('Seeding rule deleted');
        this.reloadSeedingRules(clientId);
      },
      error: () => this.toast.error('Failed to delete seeding rule'),
    });
  }

  private reloadSeedingRules(clientId: string): void {
    this.api.getSeedingRules(clientId).subscribe({
      next: (rules) => {
        this.clientConfigs.update(configs =>
          configs.map(c => c.downloadClientId === clientId ? { ...c, seedingRules: rules } : c)
        );
      },
      error: () => this.toast.error('Failed to reload seeding rules'),
    });
  }

  // --- Unlinked config ---

  updateUnlinkedField(field: string, value: any): void {
    this.updateSelectedClient(client => ({
      ...client,
      unlinkedConfig: {
        ...(client.unlinkedConfig ?? createDefaultUnlinkedConfig()),
        [field]: value,
      },
    }));
  }

  saveUnlinkedConfig(): void {
    const clientId = this.selectedClientId();
    const client = this.selectedClient();
    if (!clientId || !client?.unlinkedConfig) return;

    this.unlinkedSaving.set(true);
    this.api.updateUnlinkedConfig(clientId, client.unlinkedConfig).subscribe({
      next: () => {
        this.toast.success('Unlinked config saved');
        this.unlinkedSaving.set(false);
        this.unlinkedSaved.set(true);
        setTimeout(() => this.unlinkedSaved.set(false), 1500);
        // Update snapshot for this client
        this.unlinkedSnapshots.update(s => ({
          ...s,
          [clientId]: JSON.stringify(client.unlinkedConfig),
        }));
      },
      error: (err: ApiError) => {
        this.toast.error(err.statusCode === 400 ? err.message : 'Failed to save unlinked config');
        this.unlinkedSaving.set(false);
      },
    });
  }

  private updateSelectedClient(updater: (client: ClientCleanerConfig) => ClientCleanerConfig): void {
    const id = this.selectedClientId();
    if (!id) return;
    this.clientConfigs.update(configs =>
      configs.map(c => c.downloadClientId === id ? updater(c) : c)
    );
  }

  // --- Global config save ---

  save(): void {
    if (!this.config) return;

    const jobSchedule = { every: (this.scheduleEvery() as number) ?? 5, type: this.scheduleUnit() as ScheduleUnit };
    const cronExpression = this.useAdvancedScheduling()
      ? this.cronExpression()
      : generateCronExpression(jobSchedule);

    const config = {
      enabled: this.enabled(),
      cronExpression,
      useAdvancedScheduling: this.useAdvancedScheduling(),
      ignoredDownloads: this.ignoredDownloads(),
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
    });
  }

  readonly dirty = computed(() => {
    const saved = this.savedSnapshot();
    return saved !== '' && saved !== this.buildSnapshot();
  });

  hasPendingChanges(): boolean {
    return this.dirty() || this.unlinkedDirty();
  }
}
