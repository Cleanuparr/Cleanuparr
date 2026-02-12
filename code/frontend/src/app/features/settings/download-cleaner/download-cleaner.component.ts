import { Component, ChangeDetectionStrategy, inject, signal, computed, OnInit, viewChildren, effect, untracked } from '@angular/core';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import {
  CardComponent, ButtonComponent, InputComponent, ToggleComponent,
  NumberInputComponent, SelectComponent, ChipInputComponent, AccordionComponent,
  EmptyStateComponent, LoadingStateComponent, type SelectOption,
} from '@ui';
import { DownloadCleanerApi } from '@core/api/download-cleaner.api';
import { ToastService } from '@core/services/toast.service';
import { DownloadCleanerConfig, CleanCategory, createDefaultCategory } from '@shared/models/download-cleaner-config.model';
import { ScheduleOptions } from '@shared/models/queue-cleaner-config.model';
import { ScheduleUnit } from '@shared/models/enums';
import { HasPendingChanges } from '@core/guards/pending-changes.guard';
import { DeferredLoader } from '@shared/utils/loading.util';
import { generateCronExpression, parseCronToJobSchedule } from '@shared/utils/schedule.util';

const SCHEDULE_UNIT_OPTIONS: SelectOption[] = [
  { label: 'Seconds', value: ScheduleUnit.Seconds },
  { label: 'Minutes', value: ScheduleUnit.Minutes },
  { label: 'Hours', value: ScheduleUnit.Hours },
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
  readonly loader = new DeferredLoader();
  readonly loadError = signal(false);
  readonly saving = signal(false);
  readonly saved = signal(false);

  readonly enabled = signal(false);
  readonly useAdvancedScheduling = signal(false);
  readonly cronExpression = signal('');
  readonly scheduleEvery = signal<unknown>(5);
  readonly scheduleUnit = signal<unknown>(ScheduleUnit.Minutes);
  readonly deletePrivate = signal(false);

  readonly scheduleIntervalOptions = computed(() => {
    const unit = this.scheduleUnit() as ScheduleUnit;
    const values = ScheduleOptions[unit] ?? [];
    return values.map(v => ({ label: `${v}`, value: v }));
  });
  readonly ignoredDownloads = signal<string[]>([]);
  readonly categories = signal<CleanCategory[]>([]);
  readonly categoriesExpanded = signal(true);

  // Unlinked
  readonly unlinkedEnabled = signal(false);
  readonly unlinkedTargetCategory = signal('');
  readonly unlinkedUseTag = signal(false);
  readonly unlinkedIgnoredRootDirs = signal<string[]>([]);
  readonly unlinkedCategories = signal<string[]>([]);
  readonly unlinkedExpanded = signal(false);

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

  readonly unlinkedTargetCategoryError = computed(() => {
    if (this.unlinkedEnabled() && !this.unlinkedTargetCategory().trim()) return 'Target category is required';
    return undefined;
  });

  readonly unlinkedCategoriesError = computed(() => {
    if (this.unlinkedEnabled() && this.unlinkedCategories().length === 0) {
      return 'At least one category is required when unlinked download handling is enabled';
    }
    return undefined;
  });

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

  readonly hasErrors = computed(() => {
    if (this.scheduleEveryError()) return true;
    if (this.cronError()) return true;
    if (this.unlinkedTargetCategoryError()) return true;
    if (this.unlinkedCategoriesError()) return true;
    if (this.chipInputs().some(c => c.hasUncommittedInput())) return true;
    for (const cat of this.categories()) {
      if (this.categoryNameError(cat) || this.categoryDisabledError(cat)) return true;
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
        this.deletePrivate.set(config.deletePrivate);
        this.ignoredDownloads.set(config.ignoredDownloads ?? []);
        this.categories.set(config.categories ?? []);
        this.unlinkedEnabled.set(config.unlinkedEnabled);
        this.unlinkedTargetCategory.set(config.unlinkedTargetCategory ?? '');
        this.unlinkedUseTag.set(config.unlinkedUseTag);
        this.unlinkedIgnoredRootDirs.set(config.unlinkedIgnoredRootDirs ?? []);
        this.unlinkedCategories.set(config.unlinkedCategories ?? []);
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
    this.categories.update((cats) => [...cats, createDefaultCategory()]);
  }

  removeCategory(index: number): void {
    this.categories.update((cats) => cats.filter((_, i) => i !== index));
  }

  updateCategory(index: number, field: keyof CleanCategory, value: any): void {
    this.categories.update((cats) => {
      const updated = [...cats];
      updated[index] = { ...updated[index], [field]: value };
      return updated;
    });
  }

  save(): void {
    if (!this.config) return;

    const jobSchedule = { every: (this.scheduleEvery() as number) ?? 5, type: this.scheduleUnit() as ScheduleUnit };
    const cronExpression = this.useAdvancedScheduling()
      ? this.cronExpression()
      : generateCronExpression(jobSchedule);

    const config: DownloadCleanerConfig = {
      ...this.config,
      enabled: this.enabled(),
      useAdvancedScheduling: this.useAdvancedScheduling(),
      cronExpression,
      deletePrivate: this.deletePrivate(),
      ignoredDownloads: this.ignoredDownloads(),
      categories: this.categories(),
      unlinkedEnabled: this.unlinkedEnabled(),
      unlinkedTargetCategory: this.unlinkedTargetCategory(),
      unlinkedUseTag: this.unlinkedUseTag(),
      unlinkedIgnoredRootDirs: this.unlinkedIgnoredRootDirs(),
      unlinkedCategories: this.unlinkedCategories(),
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
      error: () => {
        this.toast.error('Failed to save download cleaner settings');
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
      deletePrivate: this.deletePrivate(),
      ignoredDownloads: this.ignoredDownloads(),
      categories: this.categories(),
      unlinkedEnabled: this.unlinkedEnabled(),
      unlinkedTargetCategory: this.unlinkedTargetCategory(),
      unlinkedUseTag: this.unlinkedUseTag(),
      unlinkedIgnoredRootDirs: this.unlinkedIgnoredRootDirs(),
      unlinkedCategories: this.unlinkedCategories(),
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
