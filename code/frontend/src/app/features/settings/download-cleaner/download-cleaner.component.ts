import { Component, ChangeDetectionStrategy, inject, signal, computed, viewChildren, effect, untracked } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { form, required, min, minLength, validate, FormField } from '@angular/forms/signals';
import { NgIconComponent } from '@ng-icons/core';
import { CdkDragDrop, CdkDropList, CdkDrag, CdkDragHandle, moveItemInArray } from '@angular/cdk/drag-drop';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import {
  CardComponent, ButtonComponent, InputComponent, ToggleComponent,
  NumberInputComponent, SelectComponent, ChipInputComponent, AccordionComponent,
  EmptyStateComponent, LoadingStateComponent, ModalComponent, BadgeComponent, SpinnerComponent,
  TooltipComponent,
  type SelectOption,
} from '@ui';
import { DownloadCleanerApi } from '@core/api/download-cleaner.api';
import { ApiError } from '@core/interceptors/error.interceptor';
import { ToastService } from '@core/services/toast.service';
import { ConfirmService } from '@core/services/confirm.service';
import {
  DownloadCleanerConfig, SeedingRule, ClientCleanerConfig, UnlinkedConfigModel,
  DeadTorrentConfigModel, OrphanedFilesConfig,
  createDefaultUnlinkedConfig, createDefaultDeadTorrentConfig, createDefaultOrphanedFilesConfig,
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

interface DownloadCleanerGlobalFormModel {
  enabled: boolean;
  useAdvancedScheduling: boolean;
  cronExpression: string;
  scheduleEvery: number;
  scheduleUnit: ScheduleUnit;
  ignoredDownloads: string[];
}

interface SeedingRuleFormModel {
  name: string;
  categories: string[];
  trackerPatterns: string[];
  tagsAny: string[];
  tagsAll: string[];
  privacyType: TorrentPrivacyType;
  maxRatio: number | null;
  minSeedTime: number | null;
  maxSeedTime: number | null;
  minSeeders: number | null;
  deleteSourceFiles: boolean;
}

@Component({
  selector: 'app-download-cleaner',
  standalone: true,
  imports: [
    NgIconComponent,
    CdkDropList, CdkDrag, CdkDragHandle,
    PageHeaderComponent, CardComponent, ButtonComponent, InputComponent,
    ToggleComponent, NumberInputComponent, SelectComponent, ChipInputComponent, AccordionComponent,
    EmptyStateComponent, LoadingStateComponent, ModalComponent, BadgeComponent, SpinnerComponent,
    TooltipComponent, FormField,
  ],
  templateUrl: './download-cleaner.component.html',
  styleUrl: './download-cleaner.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DownloadCleanerComponent implements HasPendingChanges {
  private readonly api = inject(DownloadCleanerApi);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmService);
  private readonly chipInputs = viewChildren(ChipInputComponent);
  private readonly ruleChipInputs = viewChildren<ChipInputComponent>('ruleChipInput');

  readonly ruleHasUncommittedInputs = computed(() =>
    this.ruleChipInputs().some(c => c.hasUncommittedInput())
  );

  private readonly savedSnapshot = signal('');
  private readonly orphanedFilesSnapshots = signal<Record<string, string>>({});

  readonly scheduleUnitOptions = SCHEDULE_UNIT_OPTIONS;
  readonly privacyTypeOptions = PRIVACY_TYPE_OPTIONS;

  private readonly configResource = rxResource({
    stream: () => this.api.getConfig(),
  });

  readonly loader = new DeferredLoader();
  readonly loadError = computed(() => !!this.configResource.error());
  readonly saving = signal(false);
  readonly saved = signal(false);
  readonly unlinkedSaving = signal(false);
  readonly unlinkedSaved = signal(false);
  readonly deadTorrentSaving = signal(false);
  readonly deadTorrentSaved = signal(false);
  readonly orphanedFilesSaving = signal(false);
  readonly orphanedFilesSaved = signal(false);
  readonly rulesReloading = signal(false);
  private readonly unlinkedSnapshots = signal<Record<string, string>>({});
  private readonly deadTorrentSnapshots = signal<Record<string, string>>({});

  // Global settings
  private readonly model = signal<DownloadCleanerGlobalFormModel>({
    enabled: false,
    useAdvancedScheduling: false,
    cronExpression: '',
    scheduleEvery: 5,
    scheduleUnit: ScheduleUnit.Minutes,
    ignoredDownloads: [],
  });

  readonly dcForm = form(this.model, (p) => {
    validate(p.scheduleEvery, () => {
      const m = this.model();
      if (m.useAdvancedScheduling) {
        return undefined;
      }
      const options = ScheduleOptions[m.scheduleUnit] ?? [];
      return options.includes(m.scheduleEvery) ? undefined : { kind: 'schedule', message: 'Please select a value' };
    });
    validate(p.cronExpression, () => {
      const m = this.model();
      return m.useAdvancedScheduling && !m.cronExpression.trim()
        ? { kind: 'required', message: 'Cron expression is required' }
        : undefined;
    });
  });

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

  readonly isSelectedClientTransmission = computed(() =>
    this.selectedClient()?.downloadClientTypeName === DownloadClientTypeName.Transmission
  );

  readonly isTagFilterableClient = computed(() => {
    const typeName = this.selectedClient()?.downloadClientTypeName;
    return typeName === DownloadClientTypeName.qBittorrent || typeName === DownloadClientTypeName.Transmission;
  });

  readonly isSeedersFilterableClient = computed(() => {
    const typeName = this.selectedClient()?.downloadClientTypeName;
    return typeName === DownloadClientTypeName.qBittorrent
      || typeName === DownloadClientTypeName.Deluge
      || typeName === DownloadClientTypeName.Transmission
      || typeName === DownloadClientTypeName.uTorrent;
  });

  // Dead torrent detection needs a seeder count; rTorrent does not report one.
  readonly isDeadTorrentCapableClient = computed(() => {
    const typeName = this.selectedClient()?.downloadClientTypeName;
    return typeName === DownloadClientTypeName.qBittorrent
      || typeName === DownloadClientTypeName.Deluge
      || typeName === DownloadClientTypeName.Transmission
      || typeName === DownloadClientTypeName.uTorrent;
  });

  readonly seedingRulesExpanded = signal(false);
  readonly unlinkedExpanded = signal(false);
  readonly deadTorrentExpanded = signal(false);
  readonly orphanedFilesExpanded = signal(false);

  // Seeding rule modal
  readonly ruleModalVisible = signal(false);
  readonly editingRule = signal<SeedingRule | null>(null);
  private readonly ruleDefaults: SeedingRuleFormModel = {
    name: '', categories: [], trackerPatterns: [], tagsAny: [], tagsAll: [],
    privacyType: TorrentPrivacyType.Public, maxRatio: -1, minSeedTime: 0,
    maxSeedTime: -1, minSeeders: 0, deleteSourceFiles: true,
  };
  readonly ruleModel = signal<SeedingRuleFormModel>({ ...this.ruleDefaults });
  readonly ruleForm = form(this.ruleModel, (p) => {
    required(p.name, { message: 'Name is required' });
    minLength(p.categories, 1, { message: 'At least one category is required' });
    min(p.maxRatio, -1);
    min(p.minSeedTime, 0);
    min(p.maxSeedTime, -1);
    min(p.minSeeders, 0);
    validate(p.maxSeedTime, () => {
      const m = this.ruleModel();
      return (m.maxRatio ?? -1) < 0 && (m.maxSeedTime ?? -1) < 0
        ? { kind: 'disabled', message: 'Both max ratio and max seed time cannot be disabled at the same time' }
        : undefined;
    });
  });

  readonly ruleDisabledError = computed(() =>
    this.ruleForm.maxSeedTime().errors().find(e => e.kind === 'disabled')?.message
  );

  readonly scheduleIntervalOptions = computed(() => {
    const values = ScheduleOptions[this.model().scheduleUnit] ?? [];
    return values.map(v => ({ label: `${v}`, value: v }));
  });

  constructor() {
    effect(() => {
      const unit = this.model().scheduleUnit;
      const options = ScheduleOptions[unit] ?? [];
      const current = this.model().scheduleEvery;
      if (options.length > 0 && !options.includes(current)) {
        untracked(() => this.model.update(m => ({ ...m, scheduleEvery: options[0] })));
      }
    });

    effect(() => {
      const dc = this.configResource.hasValue() ? this.configResource.value() : undefined;
      if (!dc) {
        return;
      }
      untracked(() => {
        this.config = dc;
        const parsed = parseCronToJobSchedule(dc.cronExpression);
        this.model.set({
          enabled: dc.enabled,
          useAdvancedScheduling: dc.useAdvancedScheduling,
          cronExpression: dc.cronExpression,
          scheduleEvery: parsed?.every ?? 5,
          scheduleUnit: parsed?.type ?? ScheduleUnit.Minutes,
          ignoredDownloads: dc.ignoredDownloads ?? [],
        });

        this.clientConfigs.set((dc.clients ?? []).map(c => ({
          ...c,
          seedingRules: c.seedingRules ?? [],
          unlinkedConfig: c.unlinkedConfig ?? createDefaultUnlinkedConfig(),
          deadTorrentConfig: c.deadTorrentConfig ?? createDefaultDeadTorrentConfig(),
          orphanedFilesConfig: c.orphanedFilesConfig ?? createDefaultOrphanedFilesConfig(),
        })));

        if (dc.clients?.length > 0) {
          this.selectedClientId.set(dc.clients[0].downloadClientId);
        }

        const unlinkedSnapshots: Record<string, string> = {};
        const deadTorrentSnapshots: Record<string, string> = {};
        const orphanedFilesSnapshots: Record<string, string> = {};
        for (const c of dc.clients ?? []) {
          unlinkedSnapshots[c.downloadClientId] = JSON.stringify(c.unlinkedConfig ?? createDefaultUnlinkedConfig());
          deadTorrentSnapshots[c.downloadClientId] = JSON.stringify(c.deadTorrentConfig ?? createDefaultDeadTorrentConfig());
          orphanedFilesSnapshots[c.downloadClientId] = JSON.stringify(c.orphanedFilesConfig ?? createDefaultOrphanedFilesConfig());
        }
        this.unlinkedSnapshots.set(unlinkedSnapshots);
        this.deadTorrentSnapshots.set(deadTorrentSnapshots);
        this.orphanedFilesSnapshots.set(orphanedFilesSnapshots);

        // Defer snapshot so constructor effects (e.g. schedule unit clamping) settle first
        queueMicrotask(() => {
          this.savedSnapshot.set(this.buildSnapshot());
        });
      });
    });

    effect(() => {
      if (this.configResource.error()) {
        this.toast.error('Failed to load download cleaner settings');
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

  readonly unlinkedCategoriesError = computed(() => {
    const client = this.selectedClient();
    if (!client?.unlinkedConfig?.enabled) {
      return undefined;
    }
    if ((client.unlinkedConfig.categories ?? []).length === 0) {
      return 'At least one category is required';
    }
    return undefined;
  });

  readonly deadTorrentCategoriesError = computed(() => {
    const client = this.selectedClient();
    if (!client?.deadTorrentConfig?.enabled) {
      return undefined;
    }
    if ((client.deadTorrentConfig.categories ?? []).length === 0) {
      return 'At least one category is required';
    }
    return undefined;
  });

  readonly deadTorrentStrikesError = computed(() => {
    const client = this.selectedClient();
    if (!client?.deadTorrentConfig?.enabled) {
      return undefined;
    }
    if ((client.deadTorrentConfig.maxStrikes ?? 0) < 3) {
      return 'Strikes must be at least 3';
    }
    return undefined;
  });

  readonly orphanedFilesScanDirsError = computed(() => {
    const client = this.selectedClient();
    if (!client?.orphanedFilesConfig?.enabled) {
      return undefined;
    }
    if ((client.orphanedFilesConfig.scanDirectories ?? []).length === 0) {
      return 'At least one scan directory is required';
    }
    return undefined;
  });

  readonly orphanedFilesOrphanedDirError = computed(() => {
    const client = this.selectedClient();
    if (!client?.orphanedFilesConfig?.enabled) {
      return undefined;
    }
    if (!client.orphanedFilesConfig.orphanedDirectory?.trim()) {
      return 'Orphaned directory is required';
    }
    return undefined;
  });

  readonly unlinkedDirty = computed(() => {
    const client = this.selectedClient();
    if (!client) {
      return false;
    }
    const saved = this.unlinkedSnapshots()[client.downloadClientId]
      ?? JSON.stringify(createDefaultUnlinkedConfig());
    return saved !== JSON.stringify(client.unlinkedConfig);
  });

  readonly deadTorrentDirty = computed(() => {
    const client = this.selectedClient();
    if (!client) {
      return false;
    }
    const saved = this.deadTorrentSnapshots()[client.downloadClientId]
      ?? JSON.stringify(createDefaultDeadTorrentConfig());
    return saved !== JSON.stringify(client.deadTorrentConfig);
  });

  readonly orphanedFilesDirty = computed(() => {
    const client = this.selectedClient();
    if (!client) {
      return false;
    }
    const saved = this.orphanedFilesSnapshots()[client.downloadClientId]
      ?? JSON.stringify(createDefaultOrphanedFilesConfig());
    return saved !== JSON.stringify(client.orphanedFilesConfig);
  });

  readonly hasGlobalErrors = computed(() =>
    this.dcForm().invalid() || this.chipInputs().some(c => c.hasUncommittedInput())
  );

  private config: DownloadCleanerConfig | null = null;

  retry(): void {
    this.configResource.reload();
  }

  // --- Seeding rule modal CRUD ---

  openRuleModal(rule?: SeedingRule): void {
    this.editingRule.set(rule ?? null);
    if (rule) {
      this.ruleModel.set({
        name: rule.name,
        categories: [...(rule.categories ?? [])],
        trackerPatterns: [...(rule.trackerPatterns ?? [])],
        tagsAny: [...(rule.tagsAny ?? [])],
        tagsAll: [...(rule.tagsAll ?? [])],
        privacyType: rule.privacyType,
        maxRatio: rule.maxRatio,
        minSeedTime: rule.minSeedTime,
        maxSeedTime: rule.maxSeedTime,
        minSeeders: rule.minSeeders ?? 0,
        deleteSourceFiles: rule.deleteSourceFiles,
      });
    } else {
      this.ruleModel.set({ ...this.ruleDefaults });
    }
    this.ruleModalVisible.set(true);
  }

  saveRule(): void {
    if (this.ruleForm().invalid() || this.ruleHasUncommittedInputs()) {
      return;
    }
    const clientId = this.selectedClientId();
    if (!clientId) {
      return;
    }

    const m = this.ruleModel();
    const sanitize = (list: string[]) => list.map(s => s.trim()).filter(s => s.length > 0);

    const dto: Partial<SeedingRule> = {
      name: m.name.trim(),
      categories: sanitize(m.categories),
      trackerPatterns: sanitize(m.trackerPatterns),
      tagsAny: sanitize(m.tagsAny),
      tagsAll: sanitize(m.tagsAll),
      privacyType: m.privacyType,
      maxRatio: m.maxRatio ?? -1,
      minSeedTime: m.minSeedTime ?? 0,
      maxSeedTime: m.maxSeedTime ?? -1,
      minSeeders: m.minSeeders ?? 0,
      deleteSourceFiles: m.deleteSourceFiles,
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
    if (!confirmed || !rule.id) {
      return;
    }
    const clientId = this.selectedClientId();
    if (!clientId) {
      return;
    }

    this.api.deleteSeedingRule(rule.id).subscribe({
      next: () => {
        this.toast.success('Seeding rule deleted');
        this.reloadSeedingRules(clientId);
      },
      error: () => this.toast.error('Failed to delete seeding rule'),
    });
  }

  onRulesReorder(event: CdkDragDrop<SeedingRule[]>): void {
    const clientId = this.selectedClientId();
    if (!clientId) {
      return;
    }

    const rules = [...(this.selectedClient()?.seedingRules ?? [])];
    moveItemInArray(rules, event.previousIndex, event.currentIndex);

    this.clientConfigs.update(configs =>
      configs.map(c => c.downloadClientId === clientId ? { ...c, seedingRules: rules } : c)
    );

    const orderedIds = rules.map(r => r.id!).filter(Boolean);
    this.api.reorderSeedingRules(clientId, orderedIds).subscribe({
      error: () => {
        this.toast.error('Failed to reorder seeding rules');
        this.reloadSeedingRules(clientId);
      },
    });
  }

  private reloadSeedingRules(clientId: string): void {
    this.rulesReloading.set(true);
    this.api.getSeedingRules(clientId).subscribe({
      next: (rules) => {
        this.clientConfigs.update(configs =>
          configs.map(c => c.downloadClientId === clientId ? { ...c, seedingRules: rules } : c)
        );
        this.rulesReloading.set(false);
      },
      error: () => {
        this.toast.error('Failed to reload seeding rules');
        this.rulesReloading.set(false);
      },
    });
  }

  async onClientChange(newClientId: unknown): Promise<void> {
    if (this.unlinkedDirty() || this.deadTorrentDirty() || this.orphanedFilesDirty()) {
      const confirmed = await this.confirm.confirm({
        title: 'Unsaved Changes',
        message: 'You have unsaved changes for this client. Discard them?',
        confirmLabel: 'Discard',
        destructive: true,
      });
      if (!confirmed) {
        return;
      }
      const currentId = this.selectedClientId();
      if (currentId) {
        this.restoreClientEditsFromSnapshot(currentId);
      }
    }
    this.selectedClientId.set(newClientId as string | null);
  }

  /** Reverts the client's unlinked/dead-torrent/orphaned edits back to their saved snapshots. */
  private restoreClientEditsFromSnapshot(clientId: string): void {
    this.clientConfigs.update(configs => configs.map(c => {
      if (c.downloadClientId !== clientId) {
        return c;
      }
      const unlinked = this.unlinkedSnapshots()[clientId];
      const deadTorrent = this.deadTorrentSnapshots()[clientId];
      const orphaned = this.orphanedFilesSnapshots()[clientId];
      return {
        ...c,
        unlinkedConfig: unlinked ? JSON.parse(unlinked) : c.unlinkedConfig,
        deadTorrentConfig: deadTorrent ? JSON.parse(deadTorrent) : c.deadTorrentConfig,
        orphanedFilesConfig: orphaned ? JSON.parse(orphaned) : c.orphanedFilesConfig,
      };
    }));
  }

  // --- Unlinked config ---

  updateUnlinkedField<K extends keyof UnlinkedConfigModel>(field: K, value: UnlinkedConfigModel[K]): void {
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
    if (!clientId || !client?.unlinkedConfig) {
      return;
    }

    this.unlinkedSaving.set(true);
    this.api.updateUnlinkedConfig(clientId, client.unlinkedConfig).subscribe({
      next: () => {
        this.toast.success('Unlinked config saved');
        this.unlinkedSaving.set(false);
        this.unlinkedSaved.set(true);
        setTimeout(() => this.unlinkedSaved.set(false), 1500);
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

  // --- Dead torrent per-client config ---

  updateDeadTorrentField<K extends keyof DeadTorrentConfigModel>(field: K, value: DeadTorrentConfigModel[K]): void {
    this.updateSelectedClient(client => ({
      ...client,
      deadTorrentConfig: {
        ...(client.deadTorrentConfig ?? createDefaultDeadTorrentConfig()),
        [field]: value,
      },
    }));
  }

  saveDeadTorrentConfig(): void {
    const clientId = this.selectedClientId();
    const client = this.selectedClient();
    if (!clientId || !client?.deadTorrentConfig) {
      return;
    }

    this.deadTorrentSaving.set(true);
    this.api.updateDeadTorrentConfig(clientId, client.deadTorrentConfig).subscribe({
      next: () => {
        this.toast.success('Dead torrent config saved');
        this.deadTorrentSaving.set(false);
        this.deadTorrentSaved.set(true);
        setTimeout(() => this.deadTorrentSaved.set(false), 1500);
        this.deadTorrentSnapshots.update(s => ({
          ...s,
          [clientId]: JSON.stringify(client.deadTorrentConfig),
        }));
      },
      error: (err: ApiError) => {
        this.toast.error(err.statusCode === 400 ? err.message : 'Failed to save dead torrent config');
        this.deadTorrentSaving.set(false);
      },
    });
  }

  // --- Orphaned files per-client config ---

  updateOrphanedFilesField<K extends keyof OrphanedFilesConfig>(field: K, value: OrphanedFilesConfig[K]): void {
    this.updateSelectedClient(client => ({
      ...client,
      orphanedFilesConfig: {
        ...(client.orphanedFilesConfig ?? createDefaultOrphanedFilesConfig()),
        [field]: value,
      },
    }));
  }

  saveOrphanedFilesConfig(): void {
    const clientId = this.selectedClientId();
    const client = this.selectedClient();
    if (!clientId || !client?.orphanedFilesConfig) {
      return;
    }
    this.orphanedFilesSaving.set(true);
    this.api.updateOrphanedFilesConfig(clientId, client.orphanedFilesConfig).subscribe({
      next: () => {
        this.toast.success('Orphaned files settings saved');
        this.orphanedFilesSaving.set(false);
        this.orphanedFilesSaved.set(true);
        setTimeout(() => this.orphanedFilesSaved.set(false), 1500);
        this.orphanedFilesSnapshots.update(s => ({
          ...s,
          [clientId]: JSON.stringify(client.orphanedFilesConfig),
        }));
      },
      error: (err: ApiError) => {
        this.toast.error(err.statusCode === 400 ? err.message : 'Failed to save orphaned files settings');
        this.orphanedFilesSaving.set(false);
      },
    });
  }

  private updateSelectedClient(updater: (client: ClientCleanerConfig) => ClientCleanerConfig): void {
    const id = this.selectedClientId();
    if (!id) {
      return;
    }
    this.clientConfigs.update(configs =>
      configs.map(c => c.downloadClientId === id ? updater(c) : c)
    );
  }

  // --- Global config save ---

  save(): void {
    if (!this.config) {
      return;
    }

    const m = this.model();
    const jobSchedule = { every: m.scheduleEvery ?? 5, type: m.scheduleUnit };
    const cronExpression = m.useAdvancedScheduling
      ? m.cronExpression
      : generateCronExpression(jobSchedule);

    const config = {
      enabled: m.enabled,
      cronExpression,
      useAdvancedScheduling: m.useAdvancedScheduling,
      ignoredDownloads: m.ignoredDownloads,
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
    return JSON.stringify(this.model());
  }

  readonly dirty = computed(() => {
    const saved = this.savedSnapshot();
    return saved !== '' && saved !== this.buildSnapshot();
  });

  hasPendingChanges(): boolean {
    return this.dirty() || this.unlinkedDirty() || this.deadTorrentDirty() || this.orphanedFilesDirty();
  }
}
