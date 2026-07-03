import { Component, ChangeDetectionStrategy, inject, signal, computed, viewChild, viewChildren, effect, untracked } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { form, required, min, max, validate, disabled, FormField } from '@angular/forms/signals';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import {
  CardComponent, ButtonComponent, InputComponent, ToggleComponent,
  NumberInputComponent, SelectComponent, ChipInputComponent, AccordionComponent,
  BadgeComponent, EmptyStateComponent, LoadingStateComponent,
  type SelectOption,
} from '@ui';
import { NgIcon } from '@ng-icons/core';
import { QueueCleanerApi } from '@core/api/queue-cleaner.api';
import { ToastService } from '@core/services/toast.service';
import { ConfirmService } from '@core/services/confirm.service';
import { QueueCleanerConfig, ScheduleOptions } from '@shared/models/queue-cleaner-config.model';
import { StallRule, SlowRule } from '@shared/models/queue-rule.model';
import { SlowRuleModalComponent } from './slow-rule-modal.component';
import { StallRuleModalComponent } from './stall-rule-modal.component';
import { ScheduleUnit, PatternMode } from '@shared/models/enums';
import { HasPendingChanges } from '@core/guards/pending-changes.guard';
import { DeferredLoader } from '@shared/utils/loading.util';
import { generateCronExpression, parseCronToJobSchedule } from '@shared/utils/schedule.util';
import { analyzeCoverage } from './coverage-analysis.util';

const PATTERN_MODE_OPTIONS: SelectOption[] = [
  { label: 'Exclude', value: PatternMode.Exclude },
  { label: 'Include', value: PatternMode.Include },
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
    AccordionComponent, BadgeComponent, EmptyStateComponent, LoadingStateComponent,
    NgIcon, FormField, SlowRuleModalComponent, StallRuleModalComponent,
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
  private readonly stallModal = viewChild(StallRuleModalComponent);
  private readonly slowModal = viewChild(SlowRuleModalComponent);

  private readonly savedSnapshot = signal('');

  readonly patternModeOptions = PATTERN_MODE_OPTIONS;
  readonly scheduleUnitOptions = SCHEDULE_UNIT_OPTIONS;
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

  readonly failedSubFieldsDisabled = computed(() => this.model().failedMaxStrikes === 0);

  readonly failedDeletePrivateDisabled = computed(() =>
    this.failedSubFieldsDisabled() || this.model().failedIgnorePrivate
  );

  readonly qcForm = form(this.model, (p) => {
    required(p.failedMaxStrikes, { message: 'This field is required' });
    min(p.failedMaxStrikes, 0, { message: 'Value cannot be negative' });
    max(p.failedMaxStrikes, 5000, { message: 'Value cannot exceed 5000' });

    required(p.metadataMaxStrikes, { message: 'This field is required' });
    min(p.metadataMaxStrikes, 0, { message: 'Value cannot be negative' });
    max(p.metadataMaxStrikes, 5000, { message: 'Value cannot exceed 5000' });

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

    validate(p.failedPatterns, ({ value, valueOf }) => {
      if (valueOf(p.failedMaxStrikes) === 0) {
        return undefined;
      }
      return valueOf(p.failedPatternMode) === PatternMode.Include && value().length === 0
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

  // Slow rules
  readonly slowRules = computed(() => this.slowRulesResource.value());
  readonly slowRulesLoading = computed(() => this.slowRulesResource.isLoading());
  readonly slowExpanded = signal(false);
  readonly slowModalVisible = signal(false);
  readonly editingSlowRule = signal<SlowRule | null>(null);

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
    this.stallModalVisible.set(true);
  }

  reloadStallRules(): void {
    this.stallRulesResource.reload();
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
    this.slowModalVisible.set(true);
  }

  reloadSlowRules(): void {
    this.slowRulesResource.reload();
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
    return this.dirty()
      || !!this.stallModal()?.hasPendingChanges()
      || !!this.slowModal()?.hasPendingChanges();
  }
}
