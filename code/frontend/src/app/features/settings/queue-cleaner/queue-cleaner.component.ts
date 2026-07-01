import { Component, ChangeDetectionStrategy, inject, signal, computed, viewChildren, effect, untracked } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { form, required, min, max, validate, disabled, FormField } from '@angular/forms/signals';
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
  readonly stallName = signal('');
  readonly stallEnabled = signal(true);
  readonly stallMaxStrikes = signal<number | null>(3);
  readonly stallPrivacyType = signal<unknown>(TorrentPrivacyType.Both);
  readonly stallMinCompletion = signal<number | null>(0);
  readonly stallMaxCompletion = signal<number | null>(100);
  readonly stallResetOnProgress = signal(false);
  readonly stallMinProgress = signal('');
  readonly stallDeletePrivate = signal(false);
  readonly stallChangeCategory = signal(false);

  // Slow rules
  readonly slowRules = computed(() => this.slowRulesResource.value());
  readonly slowRulesLoading = computed(() => this.slowRulesResource.isLoading());
  readonly slowExpanded = signal(false);
  readonly slowModalVisible = signal(false);
  readonly editingSlowRule = signal<SlowRule | null>(null);

  // Slow rule form
  readonly slowName = signal('');
  readonly slowEnabled = signal(true);
  readonly slowMaxStrikes = signal<number | null>(3);
  readonly slowMinSpeed = signal('');
  readonly slowMaxTimeHours = signal<number | null>(0);
  readonly slowPrivacyType = signal<unknown>(TorrentPrivacyType.Both);
  readonly slowMinCompletion = signal<number | null>(0);
  readonly slowMaxCompletion = signal<number | null>(100);
  readonly slowIgnoreAboveSize = signal('');
  readonly slowResetOnProgress = signal(false);
  readonly slowDeletePrivate = signal(false);
  readonly slowChangeCategory = signal(false);

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
      if (this.model().failedIgnorePrivate) {
        untracked(() => this.model.update(m => ({ ...m, failedDeletePrivate: false })));
      }
    });

    effect(() => {
      if (this.model().failedChangeCategory) {
        untracked(() => this.model.update(m => ({ ...m, failedDeletePrivate: false })));
      }
    });

    effect(() => {
      if (this.stallChangeCategory()) {
        untracked(() => this.stallDeletePrivate.set(false));
      }
    });

    effect(() => {
      if (this.slowChangeCategory()) {
        untracked(() => this.slowDeletePrivate.set(false));
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

  // Stall modal validation
  readonly stallNameError = computed(() => {
    if (!this.stallName().trim()) return 'Name is required';
    if (this.stallName().length > 100) return 'Name cannot exceed 100 characters';
    return undefined;
  });
  readonly stallMaxStrikesError = computed(() => {
    const v = this.stallMaxStrikes();
    if (v == null) return 'This field is required';
    if (v < 3) return 'Min value is 3';
    if (v > 5000) return 'Max value is 5000';
    return undefined;
  });
  readonly stallCompletionError = computed(() => {
    const min = this.stallMinCompletion() ?? 0;
    const max = this.stallMaxCompletion() ?? 100;
    if (max <= 0) return 'Max percentage must be greater than 0';
    if (max < min) return 'Max percentage must be greater than or equal to Min percentage';
    return undefined;
  });

  // Slow modal validation
  readonly slowNameError = computed(() => {
    if (!this.slowName().trim()) return 'Name is required';
    if (this.slowName().length > 100) return 'Name cannot exceed 100 characters';
    return undefined;
  });
  readonly slowMaxStrikesError = computed(() => {
    const v = this.slowMaxStrikes();
    if (v == null) return 'This field is required';
    if (v < 3) return 'Min value is 3';
    if (v > 5000) return 'Max value is 5000';
    return undefined;
  });
  readonly slowCompletionError = computed(() => {
    const min = this.slowMinCompletion() ?? 0;
    const max = this.slowMaxCompletion() ?? 100;
    if (max <= 0) return 'Max percentage must be greater than 0';
    if (max < min) return 'Max percentage must be greater than or equal to Min percentage';
    return undefined;
  });

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
      this.stallName.set(rule.name);
      this.stallEnabled.set(rule.enabled);
      this.stallMaxStrikes.set(rule.maxStrikes);
      this.stallPrivacyType.set(rule.privacyType);
      this.stallMinCompletion.set(rule.minCompletionPercentage);
      this.stallMaxCompletion.set(rule.maxCompletionPercentage);
      this.stallResetOnProgress.set(rule.resetStrikesOnProgress);
      this.stallMinProgress.set(rule.minimumProgress ?? '');
      this.stallDeletePrivate.set(rule.deletePrivateTorrentsFromClient);
      this.stallChangeCategory.set(rule.changeCategory ?? false);
    } else {
      this.stallName.set('');
      this.stallEnabled.set(true);
      this.stallMaxStrikes.set(3);
      this.stallPrivacyType.set(TorrentPrivacyType.Both);
      this.stallMinCompletion.set(0);
      this.stallMaxCompletion.set(100);
      this.stallResetOnProgress.set(false);
      this.stallMinProgress.set('');
      this.stallDeletePrivate.set(false);
      this.stallChangeCategory.set(false);
    }
    this.stallModalVisible.set(true);
  }

  saveStallRule(): void {
    if (this.stallNameError() || this.stallMaxStrikesError() || this.stallCompletionError()) return;

    const changeCategory = this.stallChangeCategory();
    const dto: CreateStallRuleDto = {
      name: this.stallName().trim(),
      enabled: this.stallEnabled(),
      maxStrikes: this.stallMaxStrikes() ?? 3,
      privacyType: this.stallPrivacyType() as TorrentPrivacyType,
      minCompletionPercentage: this.stallMinCompletion() ?? 0,
      maxCompletionPercentage: this.stallMaxCompletion() ?? 100,
      resetStrikesOnProgress: this.stallResetOnProgress(),
      minimumProgress: this.stallMinProgress().trim() || null,
      deletePrivateTorrentsFromClient: changeCategory ? false : this.stallDeletePrivate(),
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
      this.slowName.set(rule.name);
      this.slowEnabled.set(rule.enabled);
      this.slowMaxStrikes.set(rule.maxStrikes);
      this.slowMinSpeed.set(rule.minSpeed);
      this.slowMaxTimeHours.set(rule.maxTimeHours);
      this.slowPrivacyType.set(rule.privacyType);
      this.slowMinCompletion.set(rule.minCompletionPercentage);
      this.slowMaxCompletion.set(rule.maxCompletionPercentage);
      this.slowIgnoreAboveSize.set(rule.ignoreAboveSize ?? '');
      this.slowResetOnProgress.set(rule.resetStrikesOnProgress);
      this.slowDeletePrivate.set(rule.deletePrivateTorrentsFromClient);
      this.slowChangeCategory.set(rule.changeCategory ?? false);
    } else {
      this.slowName.set('');
      this.slowEnabled.set(true);
      this.slowMaxStrikes.set(3);
      this.slowMinSpeed.set('');
      this.slowMaxTimeHours.set(0);
      this.slowPrivacyType.set(TorrentPrivacyType.Both);
      this.slowMinCompletion.set(0);
      this.slowMaxCompletion.set(100);
      this.slowIgnoreAboveSize.set('');
      this.slowResetOnProgress.set(false);
      this.slowDeletePrivate.set(false);
      this.slowChangeCategory.set(false);
    }
    this.slowModalVisible.set(true);
  }

  saveSlowRule(): void {
    if (this.slowNameError() || this.slowMaxStrikesError() || this.slowCompletionError()) return;

    const changeCategory = this.slowChangeCategory();
    const dto: CreateSlowRuleDto = {
      name: this.slowName().trim(),
      enabled: this.slowEnabled(),
      maxStrikes: this.slowMaxStrikes() ?? 3,
      privacyType: this.slowPrivacyType() as TorrentPrivacyType,
      minCompletionPercentage: this.slowMinCompletion() ?? 0,
      maxCompletionPercentage: this.slowMaxCompletion() ?? 100,
      resetStrikesOnProgress: this.slowResetOnProgress(),
      minSpeed: this.slowMinSpeed().trim(),
      maxTimeHours: this.slowMaxTimeHours() ?? 0,
      ignoreAboveSize: this.slowIgnoreAboveSize().trim() || undefined,
      deletePrivateTorrentsFromClient: changeCategory ? false : this.slowDeletePrivate(),
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

  onStallPrivacyTypeChange(value: unknown): void {
    this.stallPrivacyType.set(value);
    this.stallDeletePrivate.set(false);
  }

  onSlowPrivacyTypeChange(value: unknown): void {
    this.slowPrivacyType.set(value);
    this.slowDeletePrivate.set(false);
  }
}
