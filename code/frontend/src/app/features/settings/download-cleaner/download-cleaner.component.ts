import { Component, ChangeDetectionStrategy, inject, signal, computed, viewChildren, effect, untracked, linkedSignal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { form, min, validate, FormField } from '@angular/forms/signals';
import { NgIconComponent } from '@ng-icons/core';
import { CdkDragDrop, CdkDropList, CdkDrag, CdkDragHandle, moveItemInArray } from '@angular/cdk/drag-drop';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import {
  CardComponent, ButtonComponent, InputComponent, ToggleComponent,
  NumberInputComponent, SelectComponent, ChipInputComponent, AccordionComponent,
  EmptyStateComponent, LoadingStateComponent, BadgeComponent, SpinnerComponent,
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
import { ScheduleUnit, DownloadClientTypeName } from '@shared/models/enums';
import { HasPendingChanges } from '@core/guards/pending-changes.guard';
import { DeferredLoader } from '@shared/utils/loading.util';
import { generateCronExpression, parseCronToJobSchedule } from '@shared/utils/schedule.util';
import { SeedingRuleModalComponent } from './seeding-rule-modal.component';

const SCHEDULE_UNIT_OPTIONS: SelectOption[] = [
  { label: 'Seconds', value: ScheduleUnit.Seconds },
  { label: 'Minutes', value: ScheduleUnit.Minutes },
  { label: 'Hours', value: ScheduleUnit.Hours },
];

interface DownloadCleanerGlobalFormModel {
  enabled: boolean;
  useAdvancedScheduling: boolean;
  cronExpression: string;
  scheduleEvery: number;
  scheduleUnit: ScheduleUnit;
  ignoredDownloads: string[];
}

interface UnlinkedFormModel {
  enabled: boolean;
  targetCategory: string;
  useTag: boolean;
  ignoredRootDirs: string[];
  categories: string[];
}

interface DeadTorrentFormModel {
  enabled: boolean;
  targetCategory: string;
  useTag: boolean;
  maxStrikes: number | null;
  categories: string[];
}

interface OrphanedFilesFormModel {
  enabled: boolean;
  scanDirectories: string[];
  orphanedDirectory: string;
  excludePatterns: string[];
  minFileAgeHours: number | null;
  purgeAfterHours: number | null;
}

@Component({
  selector: 'app-download-cleaner',
  standalone: true,
  imports: [
    NgIconComponent,
    CdkDropList, CdkDrag, CdkDragHandle,
    PageHeaderComponent, CardComponent, ButtonComponent, InputComponent,
    ToggleComponent, NumberInputComponent, SelectComponent, ChipInputComponent, AccordionComponent,
    EmptyStateComponent, LoadingStateComponent, BadgeComponent, SpinnerComponent,
    TooltipComponent, FormField, SeedingRuleModalComponent,
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

  private readonly savedSnapshot = signal('');
  private readonly orphanedFilesSnapshots = signal<Record<string, string>>({});

  readonly scheduleUnitOptions = SCHEDULE_UNIT_OPTIONS;

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
    validate(p.scheduleEvery, ({ value, valueOf }) => {
      if (!valueOf(p.enabled) || valueOf(p.useAdvancedScheduling)) {
        return undefined;
      }
      const options = ScheduleOptions[valueOf(p.scheduleUnit)] ?? [];
      return options.includes(value()) ? undefined : { kind: 'schedule', message: 'Please select a value' };
    });
    validate(p.cronExpression, ({ value, valueOf }) => {
      return valueOf(p.enabled) && valueOf(p.useAdvancedScheduling) && !value().trim()
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

  readonly unlinkedModel = linkedSignal<string | null, UnlinkedFormModel>({
    source: this.selectedClientId,
    computation: (id) => {
      const snap = id ? untracked(() => this.unlinkedSnapshots())[id] : undefined;
      return snap ? JSON.parse(snap) as UnlinkedFormModel : this.toUnlinkedModel(null);
    },
  });
  readonly unlinkedForm = form(this.unlinkedModel, (p) => {
    validate(p.categories, () => {
      const m = this.unlinkedModel();
      return m.enabled && m.categories.length === 0
        ? { kind: 'required', message: 'At least one category is required' }
        : undefined;
    });
  });

  readonly deadTorrentModel = linkedSignal<string | null, DeadTorrentFormModel>({
    source: this.selectedClientId,
    computation: (id) => {
      const snap = id ? untracked(() => this.deadTorrentSnapshots())[id] : undefined;
      return snap ? JSON.parse(snap) as DeadTorrentFormModel : this.toDeadTorrentModel(null);
    },
  });
  readonly deadTorrentForm = form(this.deadTorrentModel, (p) => {
    validate(p.categories, () => {
      const m = this.deadTorrentModel();
      return m.enabled && m.categories.length === 0
        ? { kind: 'required', message: 'At least one category is required' }
        : undefined;
    });
    validate(p.maxStrikes, () => {
      const m = this.deadTorrentModel();
      return m.enabled && (m.maxStrikes ?? 0) < 3
        ? { kind: 'min', message: 'Strikes must be at least 3' }
        : undefined;
    });
  });

  readonly orphanedFilesModel = linkedSignal<string | null, OrphanedFilesFormModel>({
    source: this.selectedClientId,
    computation: (id) => {
      const snap = id ? untracked(() => this.orphanedFilesSnapshots())[id] : undefined;
      return snap ? JSON.parse(snap) as OrphanedFilesFormModel : this.toOrphanedFilesModel(null);
    },
  });
  readonly orphanedFilesForm = form(this.orphanedFilesModel, (p) => {
    validate(p.scanDirectories, () => {
      const m = this.orphanedFilesModel();
      return m.enabled && m.scanDirectories.length === 0
        ? { kind: 'required', message: 'At least one scan directory is required' }
        : undefined;
    });
    validate(p.orphanedDirectory, () => {
      const m = this.orphanedFilesModel();
      return m.enabled && !m.orphanedDirectory.trim()
        ? { kind: 'required', message: 'Orphaned directory is required' }
        : undefined;
    });
    min(p.minFileAgeHours, 0);
    min(p.purgeAfterHours, 1);
  });

  private toUnlinkedModel(c: UnlinkedConfigModel | null): UnlinkedFormModel {
    const d = c ?? createDefaultUnlinkedConfig();
    return {
      enabled: d.enabled,
      targetCategory: d.targetCategory,
      useTag: d.useTag,
      ignoredRootDirs: [...(d.ignoredRootDirs ?? [])],
      categories: [...(d.categories ?? [])],
    };
  }

  private toDeadTorrentModel(c: DeadTorrentConfigModel | null): DeadTorrentFormModel {
    const d = c ?? createDefaultDeadTorrentConfig();
    return {
      enabled: d.enabled,
      targetCategory: d.targetCategory,
      useTag: d.useTag,
      maxStrikes: d.maxStrikes,
      categories: [...(d.categories ?? [])],
    };
  }

  private toOrphanedFilesModel(c: OrphanedFilesConfig | null): OrphanedFilesFormModel {
    const d = c ?? createDefaultOrphanedFilesConfig();
    return {
      enabled: d.enabled,
      scanDirectories: [...(d.scanDirectories ?? [])],
      orphanedDirectory: d.orphanedDirectory,
      excludePatterns: [...(d.excludePatterns ?? [])],
      minFileAgeHours: d.minFileAgeHours,
      purgeAfterHours: d.purgeAfterHours ?? null,
    };
  }

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

        const unlinkedSnapshots: Record<string, string> = {};
        const deadTorrentSnapshots: Record<string, string> = {};
        const orphanedFilesSnapshots: Record<string, string> = {};
        for (const c of dc.clients ?? []) {
          unlinkedSnapshots[c.downloadClientId] = JSON.stringify(this.toUnlinkedModel(c.unlinkedConfig ?? null));
          deadTorrentSnapshots[c.downloadClientId] = JSON.stringify(this.toDeadTorrentModel(c.deadTorrentConfig ?? null));
          orphanedFilesSnapshots[c.downloadClientId] = JSON.stringify(this.toOrphanedFilesModel(c.orphanedFilesConfig ?? null));
        }
        this.unlinkedSnapshots.set(unlinkedSnapshots);
        this.deadTorrentSnapshots.set(deadTorrentSnapshots);
        this.orphanedFilesSnapshots.set(orphanedFilesSnapshots);

        // Set selection after snapshots so the sub-config linkedSignals hydrate from saved state.
        if (dc.clients?.length > 0) {
          this.selectedClientId.set(dc.clients[0].downloadClientId);
        }

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

  readonly unlinkedDirty = computed(() => {
    const id = this.selectedClientId();
    if (!id) {
      return false;
    }
    const saved = this.unlinkedSnapshots()[id] ?? JSON.stringify(this.toUnlinkedModel(null));
    return saved !== JSON.stringify(this.unlinkedModel());
  });

  readonly deadTorrentDirty = computed(() => {
    const id = this.selectedClientId();
    if (!id) {
      return false;
    }
    const saved = this.deadTorrentSnapshots()[id] ?? JSON.stringify(this.toDeadTorrentModel(null));
    return saved !== JSON.stringify(this.deadTorrentModel());
  });

  readonly orphanedFilesDirty = computed(() => {
    const id = this.selectedClientId();
    if (!id) {
      return false;
    }
    const saved = this.orphanedFilesSnapshots()[id] ?? JSON.stringify(this.toOrphanedFilesModel(null));
    return saved !== JSON.stringify(this.orphanedFilesModel());
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
    this.ruleModalVisible.set(true);
  }

  onSeedingRuleSaved(): void {
    const clientId = this.selectedClientId();
    if (clientId) {
      this.reloadSeedingRules(clientId);
    }
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
    }
    // Changing the selection resets the per-client linkedSignal models to the newly
    // selected client's saved snapshot, discarding any edits on the previous client.
    this.selectedClientId.set(newClientId as string | null);
  }

  // --- Unlinked config ---

  saveUnlinkedConfig(): void {
    const clientId = this.selectedClientId();
    if (!clientId) {
      return;
    }
    const m = this.unlinkedModel();
    const dto: UnlinkedConfigModel = {
      enabled: m.enabled,
      targetCategory: m.targetCategory,
      useTag: m.useTag,
      ignoredRootDirs: m.ignoredRootDirs,
      categories: m.categories,
    };

    this.unlinkedSaving.set(true);
    this.api.updateUnlinkedConfig(clientId, dto).subscribe({
      next: () => {
        this.toast.success('Unlinked config saved');
        this.unlinkedSaving.set(false);
        this.unlinkedSaved.set(true);
        setTimeout(() => this.unlinkedSaved.set(false), 1500);
        this.unlinkedSnapshots.update(s => ({ ...s, [clientId]: JSON.stringify(m) }));
      },
      error: (err: ApiError) => {
        this.toast.error(err.statusCode === 400 ? err.message : 'Failed to save unlinked config');
        this.unlinkedSaving.set(false);
      },
    });
  }

  // --- Dead torrent per-client config ---

  saveDeadTorrentConfig(): void {
    const clientId = this.selectedClientId();
    if (!clientId) {
      return;
    }
    const m = this.deadTorrentModel();
    const dto: DeadTorrentConfigModel = {
      enabled: m.enabled,
      targetCategory: m.targetCategory,
      useTag: m.useTag,
      maxStrikes: m.maxStrikes ?? 0,
      categories: m.categories,
    };

    this.deadTorrentSaving.set(true);
    this.api.updateDeadTorrentConfig(clientId, dto).subscribe({
      next: () => {
        this.toast.success('Dead torrent config saved');
        this.deadTorrentSaving.set(false);
        this.deadTorrentSaved.set(true);
        setTimeout(() => this.deadTorrentSaved.set(false), 1500);
        this.deadTorrentSnapshots.update(s => ({ ...s, [clientId]: JSON.stringify(m) }));
      },
      error: (err: ApiError) => {
        this.toast.error(err.statusCode === 400 ? err.message : 'Failed to save dead torrent config');
        this.deadTorrentSaving.set(false);
      },
    });
  }

  // --- Orphaned files per-client config ---

  saveOrphanedFilesConfig(): void {
    const clientId = this.selectedClientId();
    if (!clientId) {
      return;
    }
    const m = this.orphanedFilesModel();
    const dto: OrphanedFilesConfig = {
      enabled: m.enabled,
      scanDirectories: m.scanDirectories,
      orphanedDirectory: m.orphanedDirectory,
      excludePatterns: m.excludePatterns,
      minFileAgeHours: m.minFileAgeHours ?? 24,
      purgeAfterHours: m.purgeAfterHours ?? undefined,
    };

    this.orphanedFilesSaving.set(true);
    this.api.updateOrphanedFilesConfig(clientId, dto).subscribe({
      next: () => {
        this.toast.success('Orphaned files settings saved');
        this.orphanedFilesSaving.set(false);
        this.orphanedFilesSaved.set(true);
        setTimeout(() => this.orphanedFilesSaved.set(false), 1500);
        this.orphanedFilesSnapshots.update(s => ({ ...s, [clientId]: JSON.stringify(m) }));
      },
      error: (err: ApiError) => {
        this.toast.error(err.statusCode === 400 ? err.message : 'Failed to save orphaned files settings');
        this.orphanedFilesSaving.set(false);
      },
    });
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
