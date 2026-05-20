import { Component, ChangeDetectionStrategy, inject, signal, computed, OnInit, effect, untracked } from '@angular/core';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import {
  CardComponent, ButtonComponent, InputComponent, ToggleComponent,
  NumberInputComponent, SelectComponent, ChipInputComponent,
  EmptyStateComponent, LoadingStateComponent,
  type SelectOption,
} from '@ui';
import { OrphanedFilesCleanerApi } from '@core/api/orphaned-files-cleaner.api';
import { ToastService } from '@core/services/toast.service';
import { OrphanedFilesCleanerConfig } from '@shared/models/orphaned-files-cleaner-config.model';
import { HasPendingChanges } from '@core/guards/pending-changes.guard';
import { DeferredLoader } from '@shared/utils/loading.util';
import { ScheduleUnit } from '@shared/models/enums';
import { generateCronExpression, parseCronToJobSchedule, ScheduleOptions } from '@shared/utils/schedule.util';

const SCHEDULE_UNIT_OPTIONS: SelectOption[] = [
  { label: 'Minutes', value: ScheduleUnit.Minutes },
  { label: 'Hours', value: ScheduleUnit.Hours },
];

@Component({
  selector: 'app-orphaned-files-cleaner',
  standalone: true,
  imports: [
    PageHeaderComponent, CardComponent, ButtonComponent, InputComponent,
    ToggleComponent, NumberInputComponent, SelectComponent, ChipInputComponent,
    EmptyStateComponent, LoadingStateComponent,
  ],
  templateUrl: './orphaned-files-cleaner.component.html',
  styleUrl: './orphaned-files-cleaner.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrphanedFilesCleanerComponent implements OnInit, HasPendingChanges {
  private readonly api = inject(OrphanedFilesCleanerApi);
  private readonly toast = inject(ToastService);

  private readonly savedSnapshot = signal('');

  readonly scheduleUnitOptions = SCHEDULE_UNIT_OPTIONS;
  readonly loader = new DeferredLoader();
  readonly loadError = signal(false);
  readonly saving = signal(false);
  readonly saved = signal(false);

  readonly enabled = signal(false);
  readonly useAdvancedScheduling = signal(false);
  readonly cronExpression = signal('0 0 * * * ?');
  readonly scheduleEvery = signal<unknown>(1);
  readonly scheduleUnit = signal<unknown>(ScheduleUnit.Hours);
  readonly scanDirectories = signal<string[]>([]);
  readonly orphanedDirectory = signal('');
  readonly downloadDirectorySource = signal('');
  readonly downloadDirectoryTarget = signal('');
  readonly excludePatterns = signal<string[]>([]);
  readonly minFileAgeMinutes = signal<number | null>(0);
  readonly maxOrphanedFilesToProcess = signal<number | null>(50);
  readonly emptyAfterXDays = signal<number | null>(null);

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

  readonly scheduleIntervalOptions = computed(() => {
    const unit = this.scheduleUnit() as ScheduleUnit;
    const values = ScheduleOptions[unit] ?? [];
    return values.map(v => ({ label: `${v}`, value: v }));
  });

  readonly cronError = computed(() => {
    if (this.useAdvancedScheduling() && !this.cronExpression().trim()) {
      return 'Cron expression is required';
    }
    return undefined;
  });

  readonly scanDirError = computed(() => {
    if (this.enabled() && this.scanDirectories().length === 0) {
      return 'At least one scan directory is required';
    }
    return undefined;
  });

  readonly hasErrors = computed(() => !!this.cronError() || !!this.scanDirError());

  ngOnInit(): void {
    this.loadConfig();
  }

  private loadConfig(): void {
    this.loader.start();
    this.api.getConfig().subscribe({
      next: (config) => {
        this.enabled.set(config.enabled);
        this.useAdvancedScheduling.set(config.useAdvancedScheduling);
        this.cronExpression.set(config.cronExpression ?? '0 0 * * * ?');
        const parsed = parseCronToJobSchedule(config.cronExpression);
        if (parsed) {
          this.scheduleEvery.set(parsed.every);
          this.scheduleUnit.set(parsed.type);
        }
        this.scanDirectories.set(config.scanDirectories ?? []);
        this.orphanedDirectory.set(config.orphanedDirectory ?? '');
        this.downloadDirectorySource.set(config.downloadDirectorySource ?? '');
        this.downloadDirectoryTarget.set(config.downloadDirectoryTarget ?? '');
        this.excludePatterns.set(config.excludePatterns ?? []);
        this.minFileAgeMinutes.set(config.minFileAgeMinutes ?? 0);
        this.maxOrphanedFilesToProcess.set(config.maxOrphanedFilesToProcess ?? 50);
        this.emptyAfterXDays.set(config.emptyAfterXDays ?? null);
        this.loader.stop();
        queueMicrotask(() => this.savedSnapshot.set(this.buildSnapshot()));
      },
      error: () => {
        this.toast.error('Failed to load orphaned files cleaner settings');
        this.loader.stop();
        this.loadError.set(true);
      },
    });
  }

  retry(): void {
    this.loadError.set(false);
    this.loadConfig();
  }

  save(): void {
    const jobSchedule = { every: (this.scheduleEvery() as number) ?? 1, type: this.scheduleUnit() as ScheduleUnit };
    const cronExpression = this.useAdvancedScheduling()
      ? this.cronExpression()
      : generateCronExpression(jobSchedule);

    const config: OrphanedFilesCleanerConfig = {
      enabled: this.enabled(),
      cronExpression,
      useAdvancedScheduling: this.useAdvancedScheduling(),
      scanDirectories: this.scanDirectories(),
      orphanedDirectory: this.orphanedDirectory() || undefined,
      downloadDirectorySource: this.downloadDirectorySource() || undefined,
      downloadDirectoryTarget: this.downloadDirectoryTarget() || undefined,
      excludePatterns: this.excludePatterns(),
      minFileAgeMinutes: this.minFileAgeMinutes() ?? 0,
      maxOrphanedFilesToProcess: this.maxOrphanedFilesToProcess() ?? 50,
      emptyAfterXDays: this.emptyAfterXDays() ?? null,
    };

    this.saving.set(true);
    this.api.updateConfig(config).subscribe({
      next: () => {
        this.toast.success('Orphaned files cleaner settings saved');
        this.saving.set(false);
        this.saved.set(true);
        setTimeout(() => this.saved.set(false), 1500);
        this.savedSnapshot.set(this.buildSnapshot());
      },
      error: () => {
        this.toast.error('Failed to save orphaned files cleaner settings');
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
      scanDirectories: this.scanDirectories(),
      orphanedDirectory: this.orphanedDirectory(),
      downloadDirectorySource: this.downloadDirectorySource(),
      downloadDirectoryTarget: this.downloadDirectoryTarget(),
      excludePatterns: this.excludePatterns(),
      minFileAgeMinutes: this.minFileAgeMinutes(),
      maxOrphanedFilesToProcess: this.maxOrphanedFilesToProcess(),
      emptyAfterXDays: this.emptyAfterXDays(),
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
