import { Component, ChangeDetectionStrategy, inject, signal, computed, OnInit, viewChildren, effect, untracked } from '@angular/core';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import {
  CardComponent, ButtonComponent, InputComponent, ToggleComponent,
  NumberInputComponent, SelectComponent, ChipInputComponent, AccordionComponent,
  BadgeComponent, ModalComponent, EmptyStateComponent,
  type SelectOption,
} from '@ui';
import { NgIcon } from '@ng-icons/core';
import { QueueCleanerApi } from '@core/api/queue-cleaner.api';
import { ToastService } from '@core/services/toast.service';
import { ConfirmService } from '@core/services/confirm.service';
import { QueueCleanerConfig, ScheduleOptions } from '@shared/models/queue-cleaner-config.model';
import { StallRule, SlowRule, CreateStallRuleDto, CreateSlowRuleDto } from '@shared/models/queue-rule.model';
import { ScheduleUnit, PatternMode, TorrentPrivacyType } from '@shared/models/enums';
import { HasPendingChanges } from '@core/guards/pending-changes.guard';
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

@Component({
  selector: 'app-queue-cleaner',
  standalone: true,
  imports: [
    PageHeaderComponent, CardComponent, ButtonComponent, InputComponent,
    ToggleComponent, NumberInputComponent, SelectComponent, ChipInputComponent,
    AccordionComponent, BadgeComponent, ModalComponent, EmptyStateComponent, NgIcon,
  ],
  templateUrl: './queue-cleaner.component.html',
  styleUrl: './queue-cleaner.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class QueueCleanerComponent implements OnInit, HasPendingChanges {
  private readonly api = inject(QueueCleanerApi);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmService);
  private readonly chipInputs = viewChildren(ChipInputComponent);

  private savedSnapshot = '';

  readonly patternModeOptions = PATTERN_MODE_OPTIONS;
  readonly privacyTypeOptions = PRIVACY_TYPE_OPTIONS;
  readonly scheduleUnitOptions = SCHEDULE_UNIT_OPTIONS;
  readonly loading = signal(false);
  readonly loadError = signal(false);
  readonly saving = signal(false);
  readonly saved = signal(false);

  readonly enabled = signal(false);
  readonly useAdvancedScheduling = signal(false);
  readonly cronExpression = signal('');
  readonly scheduleEvery = signal<unknown>(5);
  readonly scheduleUnit = signal<unknown>(ScheduleUnit.Minutes);
  readonly ignoredDownloads = signal<string[]>([]);

  readonly scheduleIntervalOptions = computed(() => {
    const unit = this.scheduleUnit() as ScheduleUnit;
    const values = ScheduleOptions[unit] ?? [];
    return values.map(v => ({ label: `${v}`, value: v }));
  });

  // Failed import
  readonly failedMaxStrikes = signal<number | null>(3);
  readonly failedIgnorePrivate = signal(false);
  readonly failedDeletePrivate = signal(false);
  readonly failedSkipNotFound = signal(false);
  readonly failedPatterns = signal<string[]>([]);
  readonly failedPatternMode = signal<unknown>(PatternMode.Exclude);
  readonly failedExpanded = signal(true);

  // Metadata
  readonly metadataMaxStrikes = signal<number | null>(3);
  readonly metadataExpanded = signal(false);

  // Stall rules
  readonly stallRules = signal<StallRule[]>([]);
  readonly stallRulesLoading = signal(false);
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

  // Slow rules
  readonly slowRules = signal<SlowRule[]>([]);
  readonly slowRulesLoading = signal(false);
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

  // Validation
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

  readonly failedMaxStrikesError = computed(() => {
    const v = this.failedMaxStrikes();
    if (v == null) return 'This field is required';
    if (v < 0) return 'Value cannot be negative';
    if (v > 5000) return 'Value cannot exceed 5000';
    return undefined;
  });

  readonly failedPatternsError = computed(() => {
    if (this.failedPatternMode() === PatternMode.Include && this.failedPatterns().length === 0) {
      return 'At least one pattern is required when using Include mode';
    }
    return undefined;
  });

  readonly metadataMaxStrikesError = computed(() => {
    const v = this.metadataMaxStrikes();
    if (v == null) return 'This field is required';
    if (v < 0) return 'Value cannot be negative';
    if (v > 5000) return 'Value cannot exceed 5000';
    return undefined;
  });

  readonly failedSubFieldsDisabled = computed(() => {
    return this.failedMaxStrikes() === 0;
  });

  readonly patternLabel = computed(() => {
    return this.failedPatternMode() === PatternMode.Include ? 'Included Patterns' : 'Excluded Patterns';
  });

  readonly patternHint = computed(() => {
    return this.failedPatternMode() === PatternMode.Include
      ? 'Only failed imports containing these patterns will be removed and everything else will be skipped'
      : 'Failed imports containing these patterns will be skipped and everything else will be removed';
  });

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
    if (max < min) return 'Max percentage must be greater than or equal to Min percentage';
    return undefined;
  });

  readonly hasErrors = computed(() => !!(
    this.scheduleEveryError() ||
    this.cronError() ||
    this.failedMaxStrikesError() ||
    this.failedPatternsError() ||
    this.metadataMaxStrikesError() ||
    this.chipInputs().some(c => c.hasUncommittedInput())
  ));

  private config: QueueCleanerConfig | null = null;

  ngOnInit(): void {
    this.loadConfig();
    this.loadStallRules();
    this.loadSlowRules();
  }

  private loadConfig(): void {
    this.loading.set(true);
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
        this.failedMaxStrikes.set(config.failedImport.maxStrikes);
        this.failedIgnorePrivate.set(config.failedImport.ignorePrivate);
        this.failedDeletePrivate.set(config.failedImport.deletePrivate);
        this.failedSkipNotFound.set(config.failedImport.skipIfNotFoundInClient);
        this.failedPatterns.set(config.failedImport.patterns ?? []);
        this.failedPatternMode.set(config.failedImport.patternMode ?? PatternMode.Exclude);
        this.metadataMaxStrikes.set(config.downloadingMetadataMaxStrikes);
        this.loading.set(false);
        this.savedSnapshot = this.buildSnapshot();
      },
      error: () => {
        this.toast.error('Failed to load queue cleaner settings');
        this.loading.set(false);
        this.loadError.set(true);
      },
    });
  }

  private loadStallRules(): void {
    this.stallRulesLoading.set(true);
    this.api.getStallRules().subscribe({
      next: (rules) => {
        this.stallRules.set(rules);
        this.stallRulesLoading.set(false);
      },
      error: () => {
        this.toast.error('Failed to load stall rules');
        this.stallRulesLoading.set(false);
      },
    });
  }

  private loadSlowRules(): void {
    this.slowRulesLoading.set(true);
    this.api.getSlowRules().subscribe({
      next: (rules) => {
        this.slowRules.set(rules);
        this.slowRulesLoading.set(false);
      },
      error: () => {
        this.toast.error('Failed to load slow rules');
        this.slowRulesLoading.set(false);
      },
    });
  }

  retry(): void {
    this.loadError.set(false);
    this.loadConfig();
    this.loadStallRules();
    this.loadSlowRules();
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
    }
    this.stallModalVisible.set(true);
  }

  saveStallRule(): void {
    if (this.stallNameError() || this.stallMaxStrikesError() || this.stallCompletionError()) return;

    const dto: CreateStallRuleDto = {
      name: this.stallName().trim(),
      enabled: this.stallEnabled(),
      maxStrikes: this.stallMaxStrikes() ?? 3,
      privacyType: this.stallPrivacyType() as TorrentPrivacyType,
      minCompletionPercentage: this.stallMinCompletion() ?? 0,
      maxCompletionPercentage: this.stallMaxCompletion() ?? 100,
      resetStrikesOnProgress: this.stallResetOnProgress(),
      minimumProgress: this.stallMinProgress().trim() || null,
      deletePrivateTorrentsFromClient: this.stallDeletePrivate(),
    };

    const editing = this.editingStallRule();
    const request = editing?.id
      ? this.api.updateStallRule(editing.id, dto)
      : this.api.createStallRule(dto);

    request.subscribe({
      next: () => {
        this.toast.success(editing ? 'Stall rule updated' : 'Stall rule created');
        this.stallModalVisible.set(false);
        this.loadStallRules();
      },
      error: () => this.toast.error('Failed to save stall rule'),
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
        this.loadStallRules();
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
    }
    this.slowModalVisible.set(true);
  }

  saveSlowRule(): void {
    if (this.slowNameError() || this.slowMaxStrikesError() || this.slowCompletionError()) return;

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
      deletePrivateTorrentsFromClient: this.slowDeletePrivate(),
    };

    const editing = this.editingSlowRule();
    const request = editing?.id
      ? this.api.updateSlowRule(editing.id, dto)
      : this.api.createSlowRule(dto);

    request.subscribe({
      next: () => {
        this.toast.success(editing ? 'Slow rule updated' : 'Slow rule created');
        this.slowModalVisible.set(false);
        this.loadSlowRules();
      },
      error: () => this.toast.error('Failed to save slow rule'),
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
        this.loadSlowRules();
      },
      error: () => this.toast.error('Failed to delete slow rule'),
    });
  }

  save(): void {
    if (!this.config) return;

    const jobSchedule = { every: (this.scheduleEvery() as number) ?? 5, type: this.scheduleUnit() as ScheduleUnit };
    const cronExpression = this.useAdvancedScheduling()
      ? this.cronExpression()
      : generateCronExpression(jobSchedule);

    const config: QueueCleanerConfig = {
      ...this.config,
      enabled: this.enabled(),
      useAdvancedScheduling: this.useAdvancedScheduling(),
      cronExpression,
      ignoredDownloads: this.ignoredDownloads(),
      failedImport: {
        maxStrikes: this.failedMaxStrikes() ?? 3,
        ignorePrivate: this.failedIgnorePrivate(),
        deletePrivate: this.failedDeletePrivate(),
        skipIfNotFoundInClient: this.failedSkipNotFound(),
        patterns: this.failedPatterns(),
        patternMode: this.failedPatternMode() as PatternMode,
      },
      downloadingMetadataMaxStrikes: this.metadataMaxStrikes() ?? 3,
    };

    this.saving.set(true);
    this.api.updateConfig(config).subscribe({
      next: () => {
        this.toast.success('Queue cleaner settings saved');
        this.saving.set(false);
        this.saved.set(true);
        setTimeout(() => this.saved.set(false), 1500);
        this.savedSnapshot = this.buildSnapshot();
      },
      error: () => {
        this.toast.error('Failed to save queue cleaner settings');
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
      failedMaxStrikes: this.failedMaxStrikes(),
      failedIgnorePrivate: this.failedIgnorePrivate(),
      failedDeletePrivate: this.failedDeletePrivate(),
      failedSkipNotFound: this.failedSkipNotFound(),
      failedPatterns: this.failedPatterns(),
      failedPatternMode: this.failedPatternMode(),
      metadataMaxStrikes: this.metadataMaxStrikes(),
    });
  }

  hasPendingChanges(): boolean {
    return this.savedSnapshot !== '' && this.savedSnapshot !== this.buildSnapshot();
  }
}
