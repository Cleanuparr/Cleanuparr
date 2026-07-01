import { Component, ChangeDetectionStrategy, inject, signal, computed, viewChildren, effect, untracked } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { form, required, min, max, maxLength, validate, disabled, FormField } from '@angular/forms/signals';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import {
  CardComponent, ButtonComponent, InputComponent, ToggleComponent,
  NumberInputComponent, SelectComponent, ChipInputComponent, AccordionComponent,
  BadgeComponent, ModalComponent, EmptyStateComponent, LoadingStateComponent,
  SizeInputComponent,
  type SelectOption, type SizeUnit,
} from '@ui';
import { NgIcon } from '@ng-icons/core';
import { QueueCleanerApi } from '@core/api/queue-cleaner.api';
import { ToastService } from '@core/services/toast.service';
import { ConfirmService } from '@core/services/confirm.service';
import { QueueCleanerConfig, ScheduleOptions } from '@shared/models/queue-cleaner-config.model';
import { StallRule, SlowRule, CreateStallRuleDto, CreateSlowRuleDto } from '@shared/models/queue-rule.model';
import { ScheduleUnit, PatternMode, TorrentPrivacyType } from '@shared/models/enums';
import { HasPendingChanges } from '@core/guards/pending-changes.guard';
import { DeferredLoader } from '@shared/utils/loading.util';
import { generateCronExpression, parseCronToJobSchedule } from '@shared/utils/schedule.util';
import { analyzeCoverage } from './coverage-analysis.util';

const PATTERN_MODE_OPTIONS: SelectOption[] = [
  { label: 'Exclude', value: PatternMode.Exclude },
  { label: 'Include', value: PatternMode.Include },
];

const PRIVACY_TYPE_OPTIONS: SelectOption[] = [
  { label: 'Public', value: TorrentPrivacyType.Public },
  { label: 'Private', value: TorrentPrivacyType.Private },
  { label: 'Both', value: TorrentPrivacyType.Both },
];

const SCHEDULE_UNIT_OPTIONS: SelectOption[] = [
  { label: 'Seconds', value: ScheduleUnit.Seconds },
  { label: 'Minutes', value: ScheduleUnit.Minutes },
  { label: 'Hours', value: ScheduleUnit.Hours },
];

interface QueueCleanerFormModel {
  enabled: boolean;
  useAdvancedScheduling: boolean;
  cronExpression: string;
  scheduleEvery: number;
  scheduleUnit: ScheduleUnit;
  ignoredDownloads: string[];
  processNoContentId: boolean;
  failedMaxStrikes: number | null;
  failedIgnorePrivate: boolean;
  failedDeletePrivate: boolean;
  failedSkipNotFound: boolean;
  failedPatterns: string[];
  failedPatternMode: PatternMode;
  failedChangeCategory: boolean;
  metadataMaxStrikes: number | null;
}

interface StallRuleFormModel {
  name: string;
  enabled: boolean;
  maxStrikes: number | null;
  privacyType: TorrentPrivacyType;
  minCompletion: number | null;
  maxCompletion: number | null;
  resetOnProgress: boolean;
  minProgress: string;
  deletePrivate: boolean;
  changeCategory: boolean;
}

interface SlowRuleFormModel {
  name: string;
  enabled: boolean;
  maxStrikes: number | null;
  minSpeed: string;
  maxTimeHours: number | null;
  privacyType: TorrentPrivacyType;
  minCompletion: number | null;
  maxCompletion: number | null;
  ignoreAboveSize: string;
  resetOnProgress: boolean;
  deletePrivate: boolean;
  changeCategory: boolean;
}

@Component({
  selector: 'app-queue-cleaner',
  standalone: true,
  imports: [
    PageHeaderComponent, CardComponent, ButtonComponent, InputComponent,
    ToggleComponent, NumberInputComponent, SelectComponent, ChipInputComponent,
    AccordionComponent, BadgeComponent, ModalComponent, EmptyStateComponent, LoadingStateComponent,
    SizeInputComponent, NgIcon, FormField,
  ],
  templateUrl: './queue-cleaner.component.html',
  styleUrl: './queue-cleaner.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class QueueCleanerComponent implements HasPendingChanges {
  private readonly api = inject(QueueCleanerApi);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmService);
  private readonly chipInputs = viewChildren(ChipInputComponent);

  private readonly savedSnapshot = signal('');

  readonly patternModeOptions = PATTERN_MODE_OPTIONS;
  readonly privacyTypeOptions = PRIVACY_TYPE_OPTIONS;
  readonly scheduleUnitOptions = SCHEDULE_UNIT_OPTIONS;
  readonly speedUnits: SizeUnit[] = [
    { label: 'KB/s', value: 'KB' },
    { label: 'MB/s', value: 'MB' },
  ];
  readonly sizeUnits: SizeUnit[] = [
    { label: 'KB', value: 'KB' },
    { label: 'MB', value: 'MB' },
  ];
  readonly sizeUnitsLarge: SizeUnit[] = [
    { label: 'MB', value: 'MB' },
    { label: 'GB', value: 'GB' },
  ];
  private readonly configResource = rxResource({
    stream: () => this.api.getConfig(),
  });
  private readonly stallRulesResource = rxResource({
    stream: () => this.api.getStallRules(),
    defaultValue: [] as StallRule[],
  });
  private readonly slowRulesResource = rxResource({
    stream: () => this.api.getSlowRules(),
    defaultValue: [] as SlowRule[],
  });

  readonly loader = new DeferredLoader();
  readonly loadError = computed(() => !!this.configResource.error());
  readonly saving = signal(false);
  readonly saved = signal(false);

  private readonly model = signal<QueueCleanerFormModel>({
    enabled: false,
    useAdvancedScheduling: false,
    cronExpression: '',
    scheduleEvery: 5,
    scheduleUnit: ScheduleUnit.Minutes,
    ignoredDownloads: [],
    processNoContentId: false,
    failedMaxStrikes: 3,
    failedIgnorePrivate: false,
    failedDeletePrivate: false,
    failedSkipNotFound: false,
    failedPatterns: [],
    failedPatternMode: PatternMode.Exclude,
    failedChangeCategory: false,
    metadataMaxStrikes: 3,
  });

  readonly qcForm = form(this.model, (p) => {
    required(p.failedMaxStrikes, { message: 'This field is required' });
    min(p.failedMaxStrikes, 0, { message: 'Value cannot be negative' });
    max(p.failedMaxStrikes, 5000, { message: 'Value cannot exceed 5000' });

    required(p.metadataMaxStrikes, { message: 'This field is required' });
    min(p.metadataMaxStrikes, 0, { message: 'Value cannot be negative' });
    max(p.metadataMaxStrikes, 5000, { message: 'Value cannot exceed 5000' });

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

    validate(p.failedPatterns, () => {
      const m = this.model();
      if (m.failedMaxStrikes === 0) {
        return undefined;
      }
      return m.failedPatternMode === PatternMode.Include && m.failedPatterns.length === 0
        ? { kind: 'required', message: 'At least one pattern is required when using Include mode' }
        : undefined;
    });

    disabled(p.failedIgnorePrivate, () => this.model().failedMaxStrikes === 0);
    disabled(p.failedChangeCategory, () => this.model().failedMaxStrikes === 0);
    disabled(p.failedDeletePrivate, () => this.failedDeletePrivateDisabled());
    disabled(p.failedSkipNotFound, () => this.model().failedMaxStrikes === 0);
    disabled(p.failedPatternMode, () => this.model().failedMaxStrikes === 0);
    disabled(p.failedPatterns, () => this.model().failedMaxStrikes === 0);
  });

  readonly scheduleIntervalOptions = computed(() => {
    const values = ScheduleOptions[this.model().scheduleUnit] ?? [];
    return values.map(v => ({ label: `${v}`, value: v }));
  });

  // UI-only expansion state
  readonly failedExpanded = signal(true);
  readonly metadataExpanded = signal(false);

  // Stall rules
  readonly stallRules = computed(() => this.stallRulesResource.value());
  readonly stallRulesLoading = computed(() => this.stallRulesResource.isLoading());
  readonly stallExpanded = signal(false);
  readonly stallModalVisible = signal(false);
  readonly editingStallRule = signal<StallRule | null>(null);

  // Stall rule form
  private readonly stallDefaults: StallRuleFormModel = {
    name: '', enabled: true, maxStrikes: 3, privacyType: TorrentPrivacyType.Both,
    minCompletion: 0, maxCompletion: 100, resetOnProgress: false, minProgress: '',
    deletePrivate: false, changeCategory: false,
  };
  readonly stallModel = signal<StallRuleFormModel>({ ...this.stallDefaults });
  readonly stallForm = form(this.stallModel, (p) => {
    required(p.name, { message: 'Name is required' });
    maxLength(p.name, 100, { message: 'Name cannot exceed 100 characters' });
    required(p.maxStrikes, { message: 'This field is required' });
    min(p.maxStrikes, 3, { message: 'Min value is 3' });
    max(p.maxStrikes, 5000, { message: 'Max value is 5000' });
    min(p.minCompletion, 0);
    max(p.minCompletion, 100);
    min(p.maxCompletion, 1);
    max(p.maxCompletion, 100);
    validate(p.maxCompletion, () => {
      const m = this.stallModel();
      const minC = m.minCompletion ?? 0;
      const maxC = m.maxCompletion ?? 100;
      if (maxC <= 0) return { kind: 'completion', message: 'Max percentage must be greater than 0' };
      if (maxC < minC) return { kind: 'completion', message: 'Max percentage must be greater than or equal to Min percentage' };
      return undefined;
    });
    disabled(p.deletePrivate, () => this.stallModel().privacyType === TorrentPrivacyType.Public);
  });

  // Slow rules
  readonly slowRules = computed(() => this.slowRulesResource.value());
  readonly slowRulesLoading = computed(() => this.slowRulesResource.isLoading());
  readonly slowExpanded = signal(false);
  readonly slowModalVisible = signal(false);
  readonly editingSlowRule = signal<SlowRule | null>(null);

  // Slow rule form
  private readonly slowDefaults: SlowRuleFormModel = {
    name: '', enabled: true, maxStrikes: 3, minSpeed: '', maxTimeHours: 0,
    privacyType: TorrentPrivacyType.Both, minCompletion: 0, maxCompletion: 100,
    ignoreAboveSize: '', resetOnProgress: false, deletePrivate: false, changeCategory: false,
  };
  readonly slowModel = signal<SlowRuleFormModel>({ ...this.slowDefaults });
  readonly slowForm = form(this.slowModel, (p) => {
    required(p.name, { message: 'Name is required' });
    maxLength(p.name, 100, { message: 'Name cannot exceed 100 characters' });
    required(p.maxStrikes, { message: 'This field is required' });
    min(p.maxStrikes, 3, { message: 'Min value is 3' });
    max(p.maxStrikes, 5000, { message: 'Max value is 5000' });
    min(p.maxTimeHours, 0);
    min(p.minCompletion, 0);
    max(p.minCompletion, 100);
    min(p.maxCompletion, 1);
    max(p.maxCompletion, 100);
    validate(p.maxCompletion, () => {
      const m = this.slowModel();
      const minC = m.minCompletion ?? 0;
      const maxC = m.maxCompletion ?? 100;
      if (maxC <= 0) return { kind: 'completion', message: 'Max percentage must be greater than 0' };
      if (maxC < minC) return { kind: 'completion', message: 'Max percentage must be greater than or equal to Min percentage' };
      return undefined;
    });
    disabled(p.deletePrivate, () => this.slowModel().privacyType === TorrentPrivacyType.Public);
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

    // These reset effects guard on the current value: model.update always creates a new object,
    // so writing unconditionally would re-trigger the effect forever (infinite loop / page freeze).
    effect(() => {
      const m = this.model();
      if (m.failedIgnorePrivate && m.failedDeletePrivate) {
        untracked(() => this.model.update(mm => ({ ...mm, failedDeletePrivate: false })));
      }
    });

    effect(() => {
      const m = this.model();
      if (m.failedChangeCategory && m.failedDeletePrivate) {
        untracked(() => this.model.update(mm => ({ ...mm, failedDeletePrivate: false })));
      }
    });

    effect(() => {
      const m = this.stallModel();
      if ((m.changeCategory || m.privacyType === TorrentPrivacyType.Public) && m.deletePrivate) {
        untracked(() => this.stallModel.update(s => ({ ...s, deletePrivate: false })));
      }
    });

    effect(() => {
      const m = this.slowModel();
      if ((m.changeCategory || m.privacyType === TorrentPrivacyType.Public) && m.deletePrivate) {
        untracked(() => this.slowModel.update(s => ({ ...s, deletePrivate: false })));
      }
    });

    effect(() => {
      const config = this.configResource.hasValue() ? this.configResource.value() : undefined;
      if (!config) {
        return;
      }
      untracked(() => {
        this.config = config;
        const parsed = parseCronToJobSchedule(config.cronExpression);
        this.model.set({
          enabled: config.enabled,
          useAdvancedScheduling: config.useAdvancedScheduling,
          cronExpression: config.cronExpression,
          scheduleEvery: parsed?.every ?? 5,
          scheduleUnit: parsed?.type ?? ScheduleUnit.Minutes,
          ignoredDownloads: config.ignoredDownloads ?? [],
          processNoContentId: config.processNoContentId,
          failedMaxStrikes: config.failedImport.maxStrikes,
          failedIgnorePrivate: config.failedImport.ignorePrivate,
          failedDeletePrivate: config.failedImport.deletePrivate,
          failedSkipNotFound: config.failedImport.skipIfNotFoundInClient,
          failedPatterns: config.failedImport.patterns ?? [],
          failedPatternMode: config.failedImport.patternMode ?? PatternMode.Exclude,
          failedChangeCategory: config.failedImport.changeCategory ?? false,
          metadataMaxStrikes: config.downloadingMetadataMaxStrikes,
        });
        this.savedSnapshot.set(this.buildSnapshot());
      });
    });

    effect(() => {
      if (this.configResource.error()) {
        this.toast.error('Failed to load queue cleaner settings');
      }
    });

    effect(() => {
      if (this.configResource.isLoading()) {
        this.loader.start();
      } else {
        this.loader.stop();
      }
    });

    effect(() => {
      if (this.stallRulesResource.error()) {
        this.toast.error('Failed to load stall rules');
      }
    });

    effect(() => {
      if (this.slowRulesResource.error()) {
        this.toast.error('Failed to load slow rules');
      }
    });
  }

  readonly failedSubFieldsDisabled = computed(() => this.model().failedMaxStrikes === 0);

  readonly failedDeletePrivateDisabled = computed(() =>
    this.failedSubFieldsDisabled() || this.model().failedIgnorePrivate
  );

  readonly patternLabel = computed(() =>
    this.model().failedPatternMode === PatternMode.Include ? 'Included Patterns' : 'Excluded Patterns'
  );

  readonly patternHint = computed(() =>
    this.model().failedPatternMode === PatternMode.Include
      ? 'Only failed imports containing these patterns will be removed and everything else will be skipped'
      : 'Failed imports containing these patterns will be skipped and everything else will be removed'
  );

  // Coverage analysis
  readonly stallCoverage = computed(() => analyzeCoverage(this.stallRules()));
  readonly slowCoverage = computed(() => analyzeCoverage(this.slowRules()));

  readonly hasErrors = computed(() =>
    this.qcForm().invalid() || this.chipInputs().some(c => c.hasUncommittedInput())
  );

  private config: QueueCleanerConfig | null = null;

  retry(): void {
    this.configResource.reload();
    this.stallRulesResource.reload();
    this.slowRulesResource.reload();
  }

  // Stall rule CRUD
  openStallModal(rule?: StallRule): void {
    this.editingStallRule.set(rule ?? null);
    if (rule) {
      this.stallModel.set({
        name: rule.name,
        enabled: rule.enabled,
        maxStrikes: rule.maxStrikes,
        privacyType: rule.privacyType,
        minCompletion: rule.minCompletionPercentage,
        maxCompletion: rule.maxCompletionPercentage,
        resetOnProgress: rule.resetStrikesOnProgress,
        minProgress: rule.minimumProgress ?? '',
        deletePrivate: rule.deletePrivateTorrentsFromClient,
        changeCategory: rule.changeCategory ?? false,
      });
    } else {
      this.stallModel.set({ ...this.stallDefaults });
    }
    this.stallModalVisible.set(true);
  }

  saveStallRule(): void {
    if (this.stallForm().invalid()) return;

    const m = this.stallModel();
    const changeCategory = m.changeCategory;
    const dto: CreateStallRuleDto = {
      name: m.name.trim(),
      enabled: m.enabled,
      maxStrikes: m.maxStrikes ?? 3,
      privacyType: m.privacyType,
      minCompletionPercentage: m.minCompletion ?? 0,
      maxCompletionPercentage: m.maxCompletion ?? 100,
      resetStrikesOnProgress: m.resetOnProgress,
      minimumProgress: m.minProgress.trim() || null,
      deletePrivateTorrentsFromClient: changeCategory ? false : m.deletePrivate,
      changeCategory,
    };

    const editing = this.editingStallRule();
    const request = editing?.id
      ? this.api.updateStallRule(editing.id, dto)
      : this.api.createStallRule(dto);

    request.subscribe({
      next: () => {
        this.toast.success(editing ? 'Stall rule updated' : 'Stall rule created');
        this.stallModalVisible.set(false);
        this.stallRulesResource.reload();
      },
      error: (e: Error) => this.toast.error(e.message),
    });
  }

  async deleteStallRule(rule: StallRule): Promise<void> {
    const confirmed = await this.confirm.confirm({
      title: 'Delete Stall Rule',
      message: `Are you sure you want to delete "${rule.name}"?`,
      confirmLabel: 'Delete',
      destructive: true,
    });
    if (!confirmed || !rule.id) return;
    this.api.deleteStallRule(rule.id).subscribe({
      next: () => {
        this.toast.success('Stall rule deleted');
        this.stallRulesResource.reload();
      },
      error: () => this.toast.error('Failed to delete stall rule'),
    });
  }

  // Slow rule CRUD
  openSlowModal(rule?: SlowRule): void {
    this.editingSlowRule.set(rule ?? null);
    if (rule) {
      this.slowModel.set({
        name: rule.name,
        enabled: rule.enabled,
        maxStrikes: rule.maxStrikes,
        minSpeed: rule.minSpeed,
        maxTimeHours: rule.maxTimeHours,
        privacyType: rule.privacyType,
        minCompletion: rule.minCompletionPercentage,
        maxCompletion: rule.maxCompletionPercentage,
        ignoreAboveSize: rule.ignoreAboveSize ?? '',
        resetOnProgress: rule.resetStrikesOnProgress,
        deletePrivate: rule.deletePrivateTorrentsFromClient,
        changeCategory: rule.changeCategory ?? false,
      });
    } else {
      this.slowModel.set({ ...this.slowDefaults });
    }
    this.slowModalVisible.set(true);
  }

  saveSlowRule(): void {
    if (this.slowForm().invalid()) return;

    const m = this.slowModel();
    const changeCategory = m.changeCategory;
    const dto: CreateSlowRuleDto = {
      name: m.name.trim(),
      enabled: m.enabled,
      maxStrikes: m.maxStrikes ?? 3,
      privacyType: m.privacyType,
      minCompletionPercentage: m.minCompletion ?? 0,
      maxCompletionPercentage: m.maxCompletion ?? 100,
      resetStrikesOnProgress: m.resetOnProgress,
      minSpeed: m.minSpeed.trim(),
      maxTimeHours: m.maxTimeHours ?? 0,
      ignoreAboveSize: m.ignoreAboveSize.trim() || undefined,
      deletePrivateTorrentsFromClient: changeCategory ? false : m.deletePrivate,
      changeCategory,
    };

    const editing = this.editingSlowRule();
    const request = editing?.id
      ? this.api.updateSlowRule(editing.id, dto)
      : this.api.createSlowRule(dto);

    request.subscribe({
      next: () => {
        this.toast.success(editing ? 'Slow rule updated' : 'Slow rule created');
        this.slowModalVisible.set(false);
        this.slowRulesResource.reload();
      },
      error: (e: Error) => this.toast.error(e.message),
    });
  }

  async deleteSlowRule(rule: SlowRule): Promise<void> {
    const confirmed = await this.confirm.confirm({
      title: 'Delete Slow Rule',
      message: `Are you sure you want to delete "${rule.name}"?`,
      confirmLabel: 'Delete',
      destructive: true,
    });
    if (!confirmed || !rule.id) return;
    this.api.deleteSlowRule(rule.id).subscribe({
      next: () => {
        this.toast.success('Slow rule deleted');
        this.slowRulesResource.reload();
      },
      error: () => this.toast.error('Failed to delete slow rule'),
    });
  }

  save(): void {
    if (!this.config) return;

    const m = this.model();
    const jobSchedule = { every: m.scheduleEvery ?? 5, type: m.scheduleUnit };
    const cronExpression = m.useAdvancedScheduling
      ? m.cronExpression
      : generateCronExpression(jobSchedule);

    const config: QueueCleanerConfig = {
      ...this.config,
      enabled: m.enabled,
      useAdvancedScheduling: m.useAdvancedScheduling,
      cronExpression,
      ignoredDownloads: m.ignoredDownloads,
      processNoContentId: m.processNoContentId,
      failedImport: {
        maxStrikes: m.failedMaxStrikes ?? 3,
        ignorePrivate: m.failedIgnorePrivate,
        deletePrivate: m.failedChangeCategory ? false : m.failedDeletePrivate,
        skipIfNotFoundInClient: m.failedSkipNotFound,
        patterns: m.failedPatterns,
        patternMode: m.failedPatternMode,
        changeCategory: m.failedChangeCategory,
      },
      downloadingMetadataMaxStrikes: m.metadataMaxStrikes ?? 3,
    };

    this.saving.set(true);
    this.api.updateConfig(config).subscribe({
      next: () => {
        this.toast.success('Queue cleaner settings saved');
        this.saving.set(false);
        this.saved.set(true);
        setTimeout(() => this.saved.set(false), 1500);
        this.savedSnapshot.set(this.buildSnapshot());
      },
      error: () => {
        this.toast.error('Failed to save queue cleaner settings');
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
}
